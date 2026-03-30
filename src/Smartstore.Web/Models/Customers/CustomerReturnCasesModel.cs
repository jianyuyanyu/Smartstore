using Smartstore.Core.Localization;
using Smartstore.Web.Models.Orders;

namespace Smartstore.Web.Models.Customers
{
    public partial class CustomerReturnCasesModel : ModelBase
    {
        public List<CustomerReturnCaseModel> ReturnCases { get; set; } = [];
    }

    public partial class CustomerReturnCaseModel : ReturnCaseModel
    {
        public int ProductId { get; set; }
        public LocalizedValue<string> ProductName { get; set; }
        public string ProductSeName { get; set; }
        public string ProductUrl { get; set; }
    }
}
