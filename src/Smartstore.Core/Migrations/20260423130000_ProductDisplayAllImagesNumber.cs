using FluentMigrator;
using Smartstore.Core.Catalog.Products;
using Smartstore.Core.Data;
using Smartstore.Core.Data.Migrations;
using Smartstore.Data.Migrations;

namespace Smartstore.Core.Migrations;

[MigrationVersion("2026-04-23 13:00:00", "Core: Product display all images number")]
internal class ProductDisplayAllImagesNumber : Migration, ILocaleResourcesProvider, IDataSeeder<SmartDbContext>
{
    const string ProductTable = nameof(Product);
    const string DisplayAllImagesNumberColumn = nameof(Product.DisplayAllImagesNumber);

    public override void Up()
    {
        if (!Schema.Table(ProductTable).Column(DisplayAllImagesNumberColumn).Exists())
        {
            Create.Column(DisplayAllImagesNumberColumn).OnTable(ProductTable)
                .AsInt32()
                .Nullable()
                .Indexed();
        }
    }

    public override void Down()
    {
        if (Schema.Table(ProductTable).Column(DisplayAllImagesNumberColumn).Exists())
        {
            Delete.Column(DisplayAllImagesNumberColumn).FromTable(ProductTable);
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
        builder.AddOrUpdate("Admin.Catalog.Products.Fields.DisplayAllImagesNumber",
            "Number from which images are only displayed on variation change",
            "Anzahl, ab der Bilder nur noch bei Variantenwechsel angezeigt werden",
            "Sets the threshold above which variant images are displayed only when the corresponding value is selected. Below this threshold, all images are displayed.",
            "Legt die Obergrenze fest, ab der Variantenbilder nur bei Auswahl des entsprechenden Werts angezeigt werden. Unterhalb dieser Grenze werden alle Bilder angezeigt.");
    }
}
