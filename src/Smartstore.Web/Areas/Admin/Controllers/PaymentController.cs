﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Admin.Models;
using Smartstore.Admin.Models.Modularity;
using Smartstore.Admin.Models.Payments;
using Smartstore.Caching;
using Smartstore.ComponentModel;
using Smartstore.Core.Checkout.Payment;
using Smartstore.Core.Localization;
using Smartstore.Core.Rules;
using Smartstore.Core.Security;
using Smartstore.Core.Stores;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Modelling.Settings;

namespace Smartstore.Admin.Controllers
{
    public class PaymentController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IProviderManager _providerManager;
        private readonly ModuleManager _moduleManager;
        private readonly IWidgetService _widgetService;
        private readonly IRuleService _ruleService;
        private readonly ICacheManager _cache;
        private readonly PaymentSettings _paymentSettings;

        public PaymentController(
            SmartDbContext db,
            ILocalizedEntityService localizedEntityService,
            IStoreMappingService storeMappingService,
            IProviderManager providerManager,
            ModuleManager moduleManager,
            IWidgetService widgetService,
            IRuleService ruleService,
            ICacheManager cache,
            PaymentSettings paymentSettings)
        {
            _db = db;
            _localizedEntityService = localizedEntityService;
            _storeMappingService = storeMappingService;
            _providerManager = providerManager;
            _moduleManager = moduleManager;
            _widgetService = widgetService;
            _ruleService = ruleService;
            _cache = cache;
            _paymentSettings = paymentSettings;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Providers));
        }

        [Permission(Permissions.Configuration.PaymentMethod.Read)]
        public IActionResult Providers()
        {
            var paymentMethodsModel = new List<PaymentMethodModel>();
            var providers = _providerManager.GetAllProviders<IPaymentMethod>();

            foreach (var provider in providers)
            {
                var model = _moduleManager.ToProviderModel<IPaymentMethod, PaymentMethodModel>(provider);
                var instance = provider.Value;

                model.IsActive = provider.IsPaymentProviderEnabled(_paymentSettings);
                model.SupportCapture = instance.SupportCapture;
                model.SupportPartiallyRefund = instance.SupportPartiallyRefund;
                model.SupportRefund = instance.SupportRefund;
                model.SupportVoid = instance.SupportVoid;
                model.RecurringPaymentTypeEnum = instance.RecurringPaymentType;
                model.RecurringPaymentType = instance.RecurringPaymentType.GetLocalizedEnum();

                paymentMethodsModel.Add(model);
            }

            return View(paymentMethodsModel);
        }

        [HttpPost]
        [Permission(Permissions.Configuration.PaymentMethod.Activate)]
        public async Task<IActionResult> ActivateProvider(string systemName, bool activate)
        {
            var provider = _providerManager.GetProvider<IPaymentMethod>(systemName);

            if (!activate)
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Remove(x => x.EqualsNoCase(provider.Metadata.SystemName));
            }
            else
            {
                _paymentSettings.ActivePaymentMethodSystemNames.Add(provider.Metadata.SystemName);
            }

            await Services.SettingFactory.SaveSettingsAsync(_paymentSettings);
            await _widgetService.ActivateWidgetAsync(provider.Metadata.SystemName, activate);

            return RedirectToAction(nameof(Providers));
        }

        [Permission(Permissions.Configuration.PaymentMethod.Read)]
        public async Task<IActionResult> Edit(string systemName)
        {
            var provider = _providerManager.GetProvider<IPaymentMethod>(systemName);
            var paymentMethod = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.PaymentMethodSystemName == systemName);

            var model = new PaymentMethodEditModel();
            var providerModel = _moduleManager.ToProviderModel<IPaymentMethod, ProviderModel>(provider, true);
            var pageTitle = providerModel.FriendlyName;

            model.SystemName = providerModel.SystemName;
            model.IconUrl = providerModel.IconUrl;
            model.FriendlyName = providerModel.FriendlyName;
            model.Description = providerModel.Description;
            model.SelectedStoreIds = await _storeMappingService.GetAuthorizedStoreIdsAsync(paymentMethod);

            if (paymentMethod != null)
            {
                model.Id = paymentMethod.Id;
                model.FullDescription = paymentMethod.FullDescription;
                model.RoundOrderTotalEnabled = paymentMethod.RoundOrderTotalEnabled;
                model.LimitedToStores = paymentMethod.LimitedToStores;
                model.SelectedRuleSetIds = paymentMethod.RuleSets.Select(x => x.Id).ToArray();
            }

            AddLocales(model.Locales, (locale, languageId) =>
            {
                locale.FriendlyName = _moduleManager.GetLocalizedFriendlyName(provider.Metadata, languageId, false);
                locale.Description = _moduleManager.GetLocalizedDescription(provider.Metadata, languageId, false);

                if (pageTitle.IsEmpty() && languageId == Services.WorkContext.WorkingLanguage.Id)
                {
                    pageTitle = locale.FriendlyName;
                }

                if (paymentMethod != null)
                {
                    locale.FullDescription = paymentMethod.GetLocalized(x => x.FullDescription, languageId, false, false);
                }
            });

            ViewBag.Title = pageTitle;

            return View(model);
        }

        [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
        [Permission(Permissions.Configuration.PaymentMethod.Update)]
        public async Task<IActionResult> Edit(string systemName, bool continueEditing, PaymentMethodEditModel model, IFormCollection form)
        {
            var provider = _providerManager.GetProvider<IPaymentMethod>(systemName);
            if (provider == null)
            {
                return NotFound();
            }

            await _moduleManager.ApplySettingAsync(provider.Metadata, "FriendlyName", model.FriendlyName);
            await _moduleManager.ApplySettingAsync(provider.Metadata, "Description", model.Description);

            var paymentMethod = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.PaymentMethodSystemName == systemName) 
                ?? new PaymentMethod { PaymentMethodSystemName = systemName };

            paymentMethod.FullDescription = model.FullDescription;
            paymentMethod.RoundOrderTotalEnabled = model.RoundOrderTotalEnabled;
            paymentMethod.LimitedToStores = model.LimitedToStores;

            if (paymentMethod.Id == 0)
            {
                // The update permission is sufficient here.
                _db.PaymentMethods.Add(paymentMethod);
                await _db.SaveChangesAsync();
            }

            // Add/remove assigned rule sets.
            await _ruleService.ApplyRuleSetMappingsAsync(paymentMethod, model.SelectedRuleSetIds);

            await _storeMappingService.ApplyStoreMappingsAsync(paymentMethod, model.SelectedStoreIds);

            foreach (var localized in model.Locales)
            {
                await _moduleManager.ApplyLocalizedValueAsync(provider.Metadata, localized.LanguageId, "FriendlyName", localized.FriendlyName);
                await _moduleManager.ApplyLocalizedValueAsync(provider.Metadata, localized.LanguageId, "Description", localized.Description);

                await _localizedEntityService.ApplyLocalizedValueAsync(paymentMethod, x => x.FullDescription, localized.FullDescription, localized.LanguageId);
            }

            await _db.SaveChangesAsync();

            await Services.EventPublisher.PublishAsync(new ModelBoundEvent(model, paymentMethod, form));
            NotifySuccess(T("Admin.Common.DataEditSuccess"));

            return continueEditing
                ? RedirectToAction(nameof(Edit), new { systemName })
                : RedirectToAction(nameof(Providers));
        }

        #region Settings

        [Permission(Permissions.Configuration.Setting.Read)]
        [LoadSetting]
        public async Task<IActionResult> PaymentSettings(PaymentSettings settings)
        {
            var model = await MapperFactory.MapAsync<PaymentSettings, PaymentSettingsModel>(settings);
            var providers = _providerManager.GetAllProviders<IPaymentMethod>();

            var selectListItems = providers
                .Where(x => x.IsPaymentProviderEnabled(settings))
                .Select(x => new SelectListItem { Text = _moduleManager.GetLocalizedFriendlyName(x.Metadata), Value = x.Metadata.SystemName })
                .ToList();

            ViewBag.ActivePaymentMethods = new MultiSelectList(selectListItems, "Value", "Text", model.ProductDetailPaymentMethodSystemNames);

            return View(model);
        }

        [Permission(Permissions.Configuration.Setting.Update)]
        [HttpPost, SaveSetting]
        public async Task<IActionResult> PaymentSettings(PaymentSettings settings, PaymentSettingsModel model)
        {
            if (!ModelState.IsValid)
            {
                return await PaymentSettings(settings);
            }

            ModelState.Clear();

            await MapperFactory.MapAsync(model, settings);

            await _cache.RemoveByPatternAsync(PaymentService.ProductDetailPaymentIconsPatternKey);

            NotifySuccess(T("Admin.Configuration.Updated"));
            return RedirectToAction(nameof(PaymentSettings));
        }

        #endregion
    }
}
