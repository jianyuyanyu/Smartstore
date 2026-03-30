using System.Runtime.Serialization;
using Smartstore.Core.Localization;
using Smartstore.Web.Models.Media;

namespace Smartstore.Web.Models.Orders;

public partial class ReturnCaseItemsModel : ModelBase
{
    public bool IsEditable { get; set; } = true;
    public bool ReturnAllItems { get; set; } = true;
    public string Warning { get; set; }

    public List<ReturnCaseItemModel> Items { get; set; } = [];

    [IgnoreDataMember]
    public bool HasItemsToReturn
        => !Items.IsNullOrEmpty() && Items.Any(x => x.MaxReturnQuantity > 0);

    [IgnoreDataMember]
    public bool HasSingleItemToReturn { get; set; }

    public partial class ReturnCaseItemModel : EntityModelBase
    {
        public int ProductId { get; set; }
        public LocalizedValue<string> ProductName { get; set; }
        public string ProductSeName { get; set; }
        public string ProductUrl { get; set; }
        public string AttributeInfo { get; set; }
        public Money UnitPrice { get; set; }
        public int Quantity { get; set; }
        public string QuantityUnit { get; set; }

        public ImageModel Image { get; set; }
        public string Warning { get; set; }

        public bool Selected { get; set; }
        public int SelectedReturnQuantity { get; set; }

        public int MaxReturnQuantity { get; set; }
        public bool CanReturnItem
            => MaxReturnQuantity > 0;

        public List<ReturnCaseModel> ReturnCases { get; set; }
    }
}
