using FluentMigrator;
using Smartstore.Core.Catalog.Categories;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Data;
using Smartstore.Core.Data.Migrations;
using Smartstore.Data.Migrations;
using RcEntity = Smartstore.Core.Checkout.Orders.ReturnCase;

namespace Smartstore.Core.Migrations;

[MigrationVersion("2026-03-12 15:00:00", "Core: Withdrawal")]
internal class Withdrawal : Migration, ILocaleResourcesProvider, IDataSeeder<SmartDbContext>
{
    public override void Up()
    {
        if (!Schema.Table(nameof(Checkout.Orders.ReturnCase)).Column(nameof(RcEntity.WithdrawalId)).Exists())
        {
            Create.Column(nameof(RcEntity.WithdrawalId)).OnTable(nameof(Checkout.Orders.ReturnCase))
                .AsInt32()
                .Nullable()
                .Indexed();
        }

        if (!Schema.Table(nameof(Order)).Column(nameof(Order.CompletedOn)).Exists())
        {
            Create.Column(nameof(Order.CompletedOn)).OnTable(nameof(Order))
                .AsDateTime2()
                .Nullable();
        }

        if (!Schema.Table(nameof(Product)).Column(nameof(Product.WithdrawalPeriodDays)).Exists())
        {
            Create.Column(nameof(Product.WithdrawalPeriodDays)).OnTable(nameof(Product))
                .AsInt32()
                .Nullable();
        }

        if (!Schema.Table(nameof(Category)).Column(nameof(Category.WithdrawalPeriodDays)).Exists())
        {
            Create.Column(nameof(Category.WithdrawalPeriodDays)).OnTable(nameof(Category))
                .AsInt32()
                .Nullable();
        }
    }

    public override void Down()
    {
        var columns = new Dictionary<string, string>
        {
            [nameof(Checkout.Orders.ReturnCase)] = nameof(RcEntity.WithdrawalId),
            [nameof(Order)] = nameof(Order.CompletedOn),
            [nameof(Product)] = nameof(Product.WithdrawalPeriodDays),
            [nameof(Category)] =  nameof(Category.WithdrawalPeriodDays)
        };

        foreach (var pair in columns)
        {
            if (Schema.Table(pair.Key).Column(pair.Value).Exists())
            {
                Delete.Column(pair.Value).FromTable(pair.Key);
            }
        }
    }

    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => false;

    public async Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
    {
        await context.MigrateLocaleResourcesAsync(MigrateLocaleResources);
    }

    public void MigrateLocaleResources(LocaleResourcesBuilder builder)
    {
        builder.AddOrUpdate("Withdrawal.WithdrawItemOnly",
            "Only withdraw this item in the ordered quantity.", 
            "Nur diesen Artikel in der bestellten Menge widerrufen.");

        builder.AddOrUpdate("Admin.Catalog.Products.Fields.WithdrawalPeriodDays",
            "Withdrawal period (in days)",
            "Widerrufsfrist (in Tagen)",
            "Specifies the number of days within which the product can be withdrawn. A value of 0 means that the product cannot be withdrawn (e.g., hygiene products)."
            + " This setting is only effective when the withdrawal plugin is used.",
            "Legt die Frist in Tagen fest, bis zu der das Produkt widerrufen werden kann. Der Wert 0 bedeutet, dass das Produkt nicht widerrufbar ist (z.B. Hygieneartikel)."
            + " Diese Einstellung ist nur wirksam, wenn das Vertragswiderrufs-Plugin verwendet wird.");

        builder.AddOrUpdate("Admin.Catalog.Categories.Fields.WithdrawalPeriodDays",
            "Withdrawal period (in days)",
            "Widerrufsfrist (in Tagen)",
            "Specifies the number of days within which products in this category can be withdrawn. A value of 0 means that the products cannot be withdrawn (e.g., hygiene products)."
            + " If a product is assigned to multiple categories, it must meet the withdrawal period for each category in order to be withdrawn."
            + " This setting is only effective when the withdrawal plugin is used.",
            "Legt die Frist in Tagen fest, innerhalb derer Produkte dieser Warengruppe widerrufen werden können. Der Wert 0 bedeutet, dass die Produkte nicht widerrufbar sind"
            + " (z.B. Hygieneartikel). Wenn ein Produkt mehreren Warengruppen zugeordnet ist, müssen die Widerrufsfristen aller Warengruppen eingehalten sein, damit der Artikel"
            + " widerrufen werden kann. Diese Einstellung ist nur wirksam, wenn das Vertragswiderrufs-Plugin verwendet wird.");
    }
}
