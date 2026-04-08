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
    const string ReturnCaseTable = nameof(Checkout.Orders.ReturnCase);
    const string WithdrawalIdColumn = nameof(RcEntity.WithdrawalId);

    public override void Up()
    {
        if (!Schema.Table(ReturnCaseTable).Column(WithdrawalIdColumn).Exists())
        {
            Create.Column(WithdrawalIdColumn).OnTable(ReturnCaseTable)
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
        // Columns
        if (Schema.Table(ReturnCaseTable).Column(WithdrawalIdColumn).Exists())
            Delete.Column(WithdrawalIdColumn).FromTable(ReturnCaseTable);

        if (Schema.Table(nameof(Order)).Column(nameof(Order.CompletedOn)).Exists())
            Delete.Column(nameof(Order.CompletedOn)).FromTable(nameof(Order));

        if (Schema.Table(nameof(Product)).Column(nameof(Product.WithdrawalPeriodDays)).Exists())
            Delete.Column(nameof(Product.WithdrawalPeriodDays)).FromTable(nameof(Product));

        if (Schema.Table(nameof(Category)).Column(nameof(Category.WithdrawalPeriodDays)).Exists())
            Delete.Column(nameof(Category.WithdrawalPeriodDays)).FromTable(nameof(Category));
    }

    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => false;

    public async Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
    {
        await context.MigrateLocaleResourcesAsync(MigrateLocaleResources);

        // Fix "RequestedActionUpdatedOnUtc" where no "RequestedAction" is set.
        await context.ReturnCases
            .Where(x => x.RequestedActionUpdatedOnUtc != null && string.IsNullOrEmpty(x.RequestedAction))
            .ExecuteUpdateAsync(x => x.SetProperty(rc => rc.RequestedActionUpdatedOnUtc, rc => null), cancelToken);
    }

    public void MigrateLocaleResources(LocaleResourcesBuilder builder)
    {
        builder.AddOrUpdate("Common.Type", "Type", "Typ");

        builder.Delete(
            "Admin.ReturnRequests.Fields.CreatedOn.Hint",
            "Admin.ReturnRequests.Fields.Status.Hint",
            "Account.CustomerReturnRequests.Title",
            "ReturnRequests.Products.RequestAlreadyExists",
            "Admin.ReturnRequests.Accept.Caption",
            "Account.CustomerOrders.ReturnItems");

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

        builder.AddOrUpdate("Enums.ReturnCaseKind.Return", "Return", "Retoure");
        builder.AddOrUpdate("Enums.ReturnCaseKind.Withdrawal", "Withdrawal", "Widerruf");

        builder.AddOrUpdate("ReturnCase.Case", "Case #{0}", "Fall #{0}");
        builder.AddOrUpdate("ReturnCase.NextStep", "Next step", "Nächster Schritt");
        builder.AddOrUpdate("ReturnCase.WithdrawalQuantity", "Withdrawal quantity", "Widerrufsmenge");
        builder.AddOrUpdate("ReturnCase.ReceivedWithdrawal", "We have received your withdrawal.", "Ihr Widerruf ist bei uns eingegangen.");
        builder.AddOrUpdate("ReturnCase.Open", "Open", "Offen");
        builder.AddOrUpdate("ReturnCase.Complete", "Complete", "Abgeschlossen");

        builder.AddOrUpdate("ReturnCase.NextStep.Pending", "Please return the items.", "Bitte senden Sie die Artikel zurück.");
        builder.AddOrUpdate("ReturnCase.NextStep.Received", "We are processing your return.", "Wir bearbeiten Ihre Rücksendung.");
        builder.AddOrUpdate("ReturnCase.NextStep.ReturnAuthorized", "We will deal with the matter further.", "Wir veranlassen die weitere Bearbeitung.");
        builder.AddOrUpdate("ReturnCase.NextStep.ItemsRepaired", "We will send the repaired items back to you.", "Wir senden Ihnen die reparierten Artikel zurück.");
        builder.AddOrUpdate("ReturnCase.NextStep.ItemsRefunded", "The refund has been processed.", "Die Erstattung wurde veranlasst.");
        builder.AddOrUpdate("ReturnCase.NextStep.RequestRejected", "No further action is required.", "Es ist keine weitere Aktion erforderlich.");
        builder.AddOrUpdate("ReturnCase.NextStep.Cancelled", "No further action is required.", "Es ist keine weitere Aktion erforderlich.");

        builder.AddOrUpdate("ReturnCase.OrderWithdrawn",
            "The order has been withdrawn.",
            "Die Bestellung wurde widerrufen.");

        builder.AddOrUpdate("ReturnCase.WithdrawalItemExists",
            "A withdrawal request has been submitted for this item.",
            "Für diesen Artikel wurde ein Widerruf eingereicht.");

        builder.AddOrUpdate("ReturnCase.ReturnItemExists",
            "This item has returns.",
            "Für diesen Artikel liegen Retouren vor.");

        builder.AddOrUpdate("ReturnCase.StartProcessing",
            "Start processing",
            "Verarbeitung starten",
            "Converts the withdrawal into a return for further processing.",
            "Wandelt den Widerruf in eine Retoure zur weiteren Bearbeitung.");
        builder.AddOrUpdate("ReturnCase.ConvertedWithdrawal",
            "The withdrawal has been converted to a return. Status: <b>{0}</b>.",
            "Der Widerruf wurde in eine Retoure umgewandelt. Status: <b>{0}</b>.");

        // Renaming, typos, fixes.
        builder.AddOrUpdate("Products.Details", "Product details", "Produktdetails");
        builder.AddOrUpdate("Account.CustomerReturnRequests", "Withdrawals and Returns", "Widerrufe und Retouren");
        builder.AddOrUpdate("Admin.ReturnRequests", "Withdrawals and Returns", "Widerrufe und Retouren");

        builder.AddOrUpdate("Enums.ReturnRequestStatus.RequestRejected")
            .Value("de", "Antrag abgewiesen");
        builder.AddOrUpdate("Enums.ReturnRequestStatus.Received", "Received items", "Ware erhalten");

        builder.AddOrUpdate("PageTitle.OrderDetails")
            .Value("de", "Bestelldetails");

        builder.AddOrUpdate("Admin.ReturnRequests.EditReturnRequestDetails", "Edit return", "Retoure bearbeiten");
        builder.AddOrUpdate("Admin.Withdrawal.EditWithdrawal", "Edit withdrawal", "Widerruf bearbeiten");

        builder.AddOrUpdate("Account.CustomerReturnRequests.Date", "Requested on", "Angefragt am");

        builder.AddOrUpdate("Admin.ReturnRequests.Fields.ID",
            "ID",
            "ID",
            "Return item ID",
            "ID des Retourenartikels");

        builder.AddOrUpdate("Admin.ReturnRequests.Fields.Quantity.Hint",
            "Number of items to be returned",
            "Anzahl der zurückzusendenden Artikel");

        builder.AddOrUpdate("Admin.ReturnRequests.Deleted",
            "The return has been deleted",
            "Die Retoure wurde gelöscht");

        builder.AddOrUpdate("Admin.ReturnRequests.MaxRefundAmount.Hint",
            "The maximum amount that can be refunded for this item.",
            "Der maximale Betrag, der für diesen Retourenartikel erstattet werden kann.");

        builder.AddOrUpdate("Admin.ReturnRequests.Updated",
            "The return has been successfully processed",
            "Der Retoure wurde erfolgreich bearbeitet");

        builder.AddOrUpdate("Admin.ReturnRequests.Accept.Caption", "Accept the return", "Retoure genehmigen");
    }
}
