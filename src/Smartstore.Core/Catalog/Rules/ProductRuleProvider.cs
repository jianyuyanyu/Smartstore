﻿using System.Globalization;
using System.Runtime.CompilerServices;
using Smartstore.Core.Catalog.Attributes;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Catalog.Search;
using Smartstore.Core.Common.Services;
using Smartstore.Core.Data;
using Smartstore.Core.Localization;
using Smartstore.Core.Rules;
using Smartstore.Core.Search;
using Smartstore.Core.Search.Facets;
using Smartstore.Core.Stores;

namespace Smartstore.Core.Catalog.Rules
{
    public partial class ProductRuleProvider : RuleProviderBase, IProductRuleProvider
    {
        internal const string VariantPrefix = "Variant";
        internal const string AttributePrefix = "Attribute";

        private readonly SmartDbContext _db;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IApplicationContext _appContext;
        private readonly IRuleService _ruleService;
        private readonly ICatalogSearchService _catalogSearchService;
        private readonly ICategoryService _categoryService;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly CatalogSettings _catalogSettings;

        public ProductRuleProvider(
            SmartDbContext db,
            IWorkContext workContext,
            IStoreContext storeContext,
            IApplicationContext appContext,
            IRuleService ruleService,
            ICatalogSearchService catalogSearchService,
            ICategoryService categoryService,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            CatalogSettings catalogSettings)
            : base(RuleScope.Product)
        {
            _db = db;
            _workContext = workContext;
            _storeContext = storeContext;
            _appContext = appContext;
            _ruleService = ruleService;
            _catalogSearchService = catalogSearchService;
            _categoryService = categoryService;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _catalogSettings = catalogSettings;
        }

        public Localizer T { get; set; } = NullLocalizer.Instance;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task<SearchFilterExpressionGroup> CreateExpressionGroupAsync(int ruleSetId)
        {
            return await _ruleService.CreateExpressionGroupAsync(ruleSetId, this) as SearchFilterExpressionGroup;
        }

        public override async Task<IRuleExpression> VisitRuleAsync(RuleEntity rule)
        {
            var expression = new SearchFilterExpression();
            await base.ConvertRuleAsync(rule, expression);
            expression.Descriptor = ((RuleExpression)expression).Descriptor as SearchFilterDescriptor;
            return expression;
        }

        public override IRuleExpressionGroup VisitRuleSet(RuleSetEntity ruleSet)
        {
            var group = new SearchFilterExpressionGroup
            {
                Id = ruleSet.Id,
                LogicalOperator = ruleSet.LogicalOperator,
                IsSubGroup = ruleSet.IsSubGroup,
                Value = ruleSet.Id,
                RawValue = ruleSet.Id.ToString(),
                Provider = this
            };

            return group;
        }

        public async Task<bool> MatchesAsync(
            int productId,
            IEnumerable<RuleSetEntity> ruleSets,
            LogicalRuleOperator logicalOperator = LogicalRuleOperator.Or)
        {
            Guard.NotZero(productId);

            if (ruleSets.IsNullOrEmpty())
            {
                return false;
            }

            var filters = await ruleSets
                .SelectAwait(x => _ruleService.CreateExpressionGroupAsync(x, this))
                .Where(x => x != null)
                .Cast<SearchFilterExpression>()
                .ToArrayAsync();
            if (filters.Length == 0)
            {
                return false;
            }

            foreach (var filter in filters)
            {
                if (filter is not SearchFilterExpressionGroup group)
                {
                    group = new SearchFilterExpressionGroup { LogicalOperator = logicalOperator };
                    group.AddExpressions([filter]);
                }

                // INFO: Same logic as in ProductRuleEvaluatorTask, one search per rule set.
                // If you want to implement this with a single search, you would have to use ICombinedSearchFilter and
                // logically combine the filters in exactly the opposite way to the way ICombinedSearchFilter is combined now
                // (AND-combine IAttributeSearchFilter and OR-combine the ICombinedSearchFilter).
                var searchQuery = new CatalogSearchQuery()
                    .OriginatesFrom("Rule/Search")
                    .WithLanguage(_workContext.WorkingLanguage)
                    .WithCurrency(_workContext.WorkingCurrency)
                    .BuildFacetMap(false)
                    .CheckSpelling(0);

                searchQuery = group.ApplyFilters(searchQuery);

                var productQuery = _catalogSearchService.PrepareQuery(searchQuery);
                if (await productQuery.AnyAsync(x => x.Id == productId))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<CatalogSearchResult> SearchAsync(SearchFilterExpression[] filters, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var searchQuery = new CatalogSearchQuery()
                .OriginatesFrom("Rule/Search")
                .WithLanguage(_workContext.WorkingLanguage)
                .WithCurrency(_workContext.WorkingCurrency)
                .BuildFacetMap(false)
                .CheckSpelling(0)
                .Slice(pageIndex * pageSize, pageSize)
                .SortBy(ProductSortingEnum.CreatedOn);

            if ((filters?.Length ?? 0) == 0)
            {
                return new CatalogSearchResult(searchQuery);
            }

            SearchFilterExpressionGroup group;

            if (filters.Length == 1 && filters[0] is SearchFilterExpressionGroup group2)
            {
                group = group2;
            }
            else
            {
                group = new SearchFilterExpressionGroup();
                group.AddExpressions(filters);
            }

            searchQuery = group.ApplyFilters(searchQuery);

            var searchResult = await _catalogSearchService.SearchAsync(searchQuery);
            return searchResult;
        }

        protected override async Task<IEnumerable<RuleDescriptor>> LoadDescriptorsAsync()
        {
            var language = _workContext.WorkingLanguage;
            var oneStarStr = T("Search.Facet.1StarAndMore").Value;
            var xStarsStr = T("Search.Facet.XStarsAndMore").Value;

            var stores = _storeContext.GetAllStores()
                .Select(x => new RuleValueSelectListOption { Value = x.Id.ToString(), Text = x.Name })
                .ToArray();

            var visibilities = ((ProductVisibility[])Enum.GetValues(typeof(ProductVisibility)))
                .Select(x => new RuleValueSelectListOption { Value = ((int)x).ToString(), Text = _localizationService.GetLocalizedEnum(x) })
                .ToArray();

            var productTypes = ((ProductType[])Enum.GetValues(typeof(ProductType)))
                .Select(x => new RuleValueSelectListOption { Value = ((int)x).ToString(), Text = _localizationService.GetLocalizedEnum(x) })
                .ToArray();

            var ratings = FacetUtility.GetRatings()
                .Reverse()
                .Skip(1)
                .Select(x => new RuleValueSelectListOption
                {
                    Value = ((double)x.Value).ToString(CultureInfo.InvariantCulture),
                    Text = (double)x.Value == 1 ? oneStarStr : xStarsStr.FormatInvariant(x.Value)
                })
                .ToArray();

            var categoryTree = _catalogSettings.ShowProductsFromSubcategories
                ? await _categoryService.GetCategoryTreeAsync(includeHidden: true)
                : null;

            #region Special filters

            CatalogSearchQuery categoryFilter(SearchFilterContext ctx, int[] categoryIds)
            {
                if (!categoryIds.IsNullOrEmpty())
                {
                    var featuredOnly = _catalogSettings.IncludeFeaturedProductsInNormalLists ? (bool?)null : false;

                    if (_catalogSettings.ShowProductsFromSubcategories)
                    {
                        if (categoryIds.Length == 1)
                        {
                            var node = categoryTree.SelectNodeById(categoryIds[0]);
                            if (node != null)
                            {
                                return ctx.Query.WithCategoryTreePath(node.GetTreePath(), featuredOnly);
                            }
                        }
                        else
                        {
                            var ids = new HashSet<int>(categoryIds);
                            foreach (var id in categoryIds)
                            {
                                var node = categoryTree.SelectNodeById(id);
                                if (node != null)
                                {
                                    ids.AddRange(node.Flatten(false).Select(y => y.Id));
                                }
                            }

                            return ctx.Query.WithCategoryIds(featuredOnly, ids.ToArray());
                        }
                    }
                    else
                    {
                        return ctx.Query.WithCategoryIds(featuredOnly, categoryIds);
                    }
                }

                return ctx.Query;
            };

            CatalogSearchQuery stockQuantityFilter(SearchFilterContext ctx, int x)
            {
                if (ctx.Expression.Operator == RuleOperator.IsEqualTo || ctx.Expression.Operator == RuleOperator.IsNotEqualTo)
                {
                    return ctx.Query.WithStockQuantity(x, x, ctx.Expression.Operator == RuleOperator.IsEqualTo, ctx.Expression.Operator == RuleOperator.IsEqualTo);
                }
                else if (ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo || ctx.Expression.Operator == RuleOperator.GreaterThan)
                {
                    return ctx.Query.WithStockQuantity(x, null, ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo, null);
                }
                else if (ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo || ctx.Expression.Operator == RuleOperator.LessThan)
                {
                    return ctx.Query.WithStockQuantity(null, x, null, ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo);
                }

                return ctx.Query;
            };

            CatalogSearchQuery priceFilter(SearchFilterContext ctx, decimal x)
            {
                if (ctx.Expression.Operator == RuleOperator.IsEqualTo || ctx.Expression.Operator == RuleOperator.IsNotEqualTo)
                {
                    return ctx.Query.PriceBetween(x, x, ctx.Expression.Operator == RuleOperator.IsEqualTo, ctx.Expression.Operator == RuleOperator.IsEqualTo);
                }
                else if (ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo || ctx.Expression.Operator == RuleOperator.GreaterThan)
                {
                    return ctx.Query.PriceBetween(x, null, ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo, null);
                }
                else if (ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo || ctx.Expression.Operator == RuleOperator.LessThan)
                {
                    return ctx.Query.PriceBetween(null, x, null, ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo);
                }

                return ctx.Query;
            };

            CatalogSearchQuery createdFilter(SearchFilterContext ctx, DateTime x)
            {
                if (ctx.Expression.Operator == RuleOperator.IsEqualTo || ctx.Expression.Operator == RuleOperator.IsNotEqualTo)
                {
                    return ctx.Query.CreatedBetween(x, x, ctx.Expression.Operator == RuleOperator.IsEqualTo, ctx.Expression.Operator == RuleOperator.IsEqualTo);
                }
                else if (ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo || ctx.Expression.Operator == RuleOperator.GreaterThan)
                {
                    return ctx.Query.CreatedBetween(x, null, ctx.Expression.Operator == RuleOperator.GreaterThanOrEqualTo, null);
                }
                else if (ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo || ctx.Expression.Operator == RuleOperator.LessThan)
                {
                    return ctx.Query.CreatedBetween(null, x, null, ctx.Expression.Operator == RuleOperator.LessThanOrEqualTo);
                }

                return ctx.Query;
            };

            #endregion

            var descriptors = new List<SearchFilterDescriptor>
            {
                new SearchFilterDescriptor<int>((ctx, x) => ctx.Query.HasStoreId(x))
                {
                    Name = "Store",
                    DisplayName = T("Admin.Rules.FilterDescriptor.Store"),
                    RuleType = RuleType.Int,
                    SelectList = new LocalRuleValueSelectList(stores),
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.AllowedCustomerRoles(x))
                {
                    Name = "CustomerRole",
                    DisplayName = T("Admin.Rules.FilterDescriptor.IsInCustomerRole"),
                    RuleType = RuleType.IntArray,
                    SelectList = new RemoteRuleValueSelectList("CustomerRole") { Multiple = true },
                    Operators = [RuleOperator.In]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.PublishedOnly(x))
                {
                    Name = "Published",
                    DisplayName = T("Admin.Catalog.Products.Fields.Published"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.AvailableOnly(x))
                {
                    Name = "AvailableByStock",
                    DisplayName = T("Products.Availability.InStock"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.AvailableByDate(x))
                {
                    Name = "AvailableByDate",
                    DisplayName = T("Admin.Rules.FilterDescriptor.AvailableByDate"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<int>((ctx, x) => ctx.Query.WithVisibility((ProductVisibility)x))
                {
                    Name = "Visibility",
                    DisplayName = T("Admin.Catalog.Products.Fields.Visibility"),
                    RuleType = RuleType.Int,
                    SelectList = new LocalRuleValueSelectList(visibilities),
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithProductIds(x))
                {
                    Name = "Product",
                    DisplayName = T("Common.Entity.Product"),
                    RuleType = RuleType.IntArray,
                    SelectList = new RemoteRuleValueSelectList("Product") { Multiple = true },
                    Operators = [RuleOperator.In]
                },
                new SearchFilterDescriptor<int>((ctx, x) => ctx.Query.IsProductType((ProductType)x))
                {
                    Name = "ProductType",
                    DisplayName = T("Admin.Catalog.Products.Fields.ProductType"),
                    RuleType = RuleType.Int,
                    SelectList = new LocalRuleValueSelectList(productTypes),
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<int[]>(categoryFilter)
                {
                    Name = "Category",
                    DisplayName = T("Common.Entity.Category"),
                    RuleType = RuleType.IntArray,
                    SelectList = new RemoteRuleValueSelectList("Category") { Multiple = true },
                    Operators = [RuleOperator.In]
                },
                new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithManufacturerIds(null, x))
                {
                    Name = "Manufacturer",
                    DisplayName = T("Common.Entity.Manufacturer"),
                    RuleType = RuleType.IntArray,
                    SelectList = new RemoteRuleValueSelectList("Manufacturer") { Multiple = true },
                    Operators = [RuleOperator.In]
                },
                // Same logic as the filter above product list.
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.HasAnyCategory(!x))
                {
                    Name = "WithoutCategory",
                    DisplayName = T("Admin.Catalog.Products.List.SearchWithoutCategories"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.HasAnyManufacturer(!x))
                {
                    Name = "WithoutManufacturer",
                    DisplayName = T("Admin.Catalog.Products.List.SearchWithoutManufacturers"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithProductTagIds(x))
                {
                    Name = "ProductTag",
                    DisplayName = T("Admin.Catalog.Products.Fields.ProductTags"),
                    RuleType = RuleType.IntArray,
                    SelectList = new RemoteRuleValueSelectList("ProductTag") { Multiple = true },
                    Operators = [RuleOperator.In]
                },
                new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithDeliveryTimeIds(x))
                {
                    Name = "DeliveryTime",
                    DisplayName = T("Admin.Catalog.Products.Fields.DeliveryTime"),
                    RuleType = RuleType.IntArray,
                    Operators = [RuleOperator.In],
                    SelectList = new RemoteRuleValueSelectList("DeliveryTime") { Multiple = true }
                },
                new SearchFilterDescriptor<int>(stockQuantityFilter)
                {
                    Name = "StockQuantity",
                    DisplayName = T("Admin.Catalog.Products.Fields.StockQuantity"),
                    RuleType = RuleType.Int
                },
                new SearchFilterDescriptor<decimal>(priceFilter)
                {
                    Name = "Price",
                    DisplayName = T("Admin.Catalog.Products.Fields.Price"),
                    RuleType = RuleType.Money,
                    Metadata = new Dictionary<string, object> { ["postfix"] = _currencyService.PrimaryCurrency.CurrencyCode }
                },
                new SearchFilterDescriptor<DateTime>(createdFilter)
                {
                    Name = "CreatedOn",
                    DisplayName = T("Common.CreatedOn"),
                    RuleType = RuleType.DateTime
                },
                new SearchFilterDescriptor<double>((ctx, x) => ctx.Query.WithRating(x, null))
                {
                    Name = "Rating",
                    DisplayName = T("Admin.Catalog.ProductReviews.Fields.Rating"),
                    RuleType = RuleType.Float,
                    Operators = [RuleOperator.GreaterThanOrEqualTo],
                    SelectList = new LocalRuleValueSelectList(ratings)
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.HomePageProductsOnly(x))
                {
                    Name = "HomepageProduct",
                    DisplayName = T("Admin.Catalog.Products.Fields.ShowOnHomePage"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.DownloadOnly(x))
                {
                    Name = "Download",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsDownload"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.RecurringOnly(x))
                {
                    Name = "Recurring",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsRecurring"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.ShipEnabledOnly(x))
                {
                    Name = "ShipEnabled",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsShipEnabled"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.FreeShippingOnly(x))
                {
                    Name = "FreeShipping",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsFreeShipping"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.TaxExemptOnly(x))
                {
                    Name = "TaxExempt",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsTaxExempt"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.EsdOnly(x))
                {
                    Name = "Esd",
                    DisplayName = T("Admin.Catalog.Products.Fields.IsEsd"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                },
                new SearchFilterDescriptor<bool>((ctx, x) => ctx.Query.HasDiscount(x))
                {
                    Name = "Discount",
                    DisplayName = T("Admin.Catalog.Products.Fields.HasDiscountsApplied"),
                    RuleType = RuleType.Boolean,
                    Operators = [RuleOperator.IsEqualTo]
                }
            };

            return descriptors.Cast<RuleDescriptor>();
        }

        protected internal IEnumerable<RuleDescriptor> LoadVariantDescriptors()
        {
            // INFO: Has to be sync unfortunately. We don't wanna break contracts.
            if (_appContext.ModuleCatalog.GetModuleByName("Smartstore.MegaSearchPlus") == null)
            {
                yield break;
            }

            // Sort by display order!
            var language = _workContext.WorkingLanguage;
            var pageIndex = -1;
            var query = _db.ProductAttributes
                .AsNoTracking()
                .Where(x => x.AllowFiltering)
                .OrderBy(x => x.DisplayOrder);

            while (true)
            {
                var variants = query.ToPagedList(++pageIndex, 1000).Load();
                foreach (var variant in variants)
                {
                    var descriptor = new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithFilter(SearchFilter.Combined("variantvalueid", CreateSearchFilter("variantvalueid", variant.Id, x))))
                    {
                        Name = $"{VariantPrefix}{variant.Id}",
                        DisplayName = variant.GetLocalized(x => x.Name, language, true, false),
                        GroupKey = "Admin.Catalog.Attributes.ProductAttributes",
                        RuleType = RuleType.IntArray,
                        SelectList = new RemoteRuleValueSelectList(KnownRuleOptionDataSourceNames.VariantValue) { Multiple = true },
                        Operators = [RuleOperator.In]
                    };
                    descriptor.Metadata["ParentId"] = variant.Id;
                    descriptor.Metadata["AllowFiltering"] = true;
                    descriptor.Metadata["ValueType"] = ProductVariantAttributeValueType.Simple;

                    yield return descriptor;
                }

                if (!variants.HasNextPage)
                {
                    yield break;
                }
            }
        }

        protected override RuleDescriptorCollection CreateDescriptorCollection(IEnumerable<RuleDescriptor> descriptors)
            => new ProductRuleDescriptorCollection(this, descriptors);

        protected internal IEnumerable<RuleDescriptor> LoadAttributeDescriptors()
        {
            // INFO: Has to be sync unfortunately. We don't wanna break contracts.
            if (_appContext.ModuleCatalog.GetModuleByName("Smartstore.MegaSearchPlus") == null)
            {
                yield break;
            }

            // Sort by display order!
            var language = _workContext.WorkingLanguage;
            var pageIndex = -1;
            var query = _db.SpecificationAttributes
                .AsNoTracking()
                .Where(x => x.AllowFiltering)
                .OrderBy(x => x.DisplayOrder);

            while (true)
            {
                var attributes = query.ToPagedList(++pageIndex, 1000).Load();
                foreach (var attribute in attributes)
                {
                    var descriptor = new SearchFilterDescriptor<int[]>((ctx, x) => ctx.Query.WithFilter(SearchFilter.Combined("attrvalueid", CreateSearchFilter("attrvalueid", attribute.Id, x))))
                    {
                        Name = $"{AttributePrefix}{attribute.Id}",
                        DisplayName = attribute.GetLocalized(x => x.Name, language, true, false),
                        GroupKey = "Admin.Catalog.Attributes.SpecificationAttributes",
                        RuleType = RuleType.IntArray,
                        SelectList = new RemoteRuleValueSelectList(KnownRuleOptionDataSourceNames.AttributeOption) { Multiple = true },
                        Operators = [RuleOperator.In]
                    };
                    descriptor.Metadata["ParentId"] = attribute.Id;

                    yield return descriptor;
                }

                if (!attributes.HasNextPage)
                {
                    yield break;
                }
            }
        }

        private static ISearchFilter[] CreateSearchFilter(string fieldName, int parentId, int[] valueIds)
        {
            return valueIds.Select(id => SearchFilter.ByField(fieldName, id).ExactMatch().NotAnalyzed().HasParent(parentId)).ToArray();
        }
    }
}
