using Microsoft.AspNetCore.Http;
using Smartstore.Events;
using Smartstore.Web.Controllers;

namespace Smartstore.Web.Modelling;

public class ModelBoundEvent : IEventMessage
{
    public ModelBoundEvent(TabbableModel boundModel, object entityModel, IFormCollection form)
    {
        BoundModel = boundModel;
        EntityModel = entityModel;
        Form = form;
    }

    public ModelBoundEvent(TabbableModel boundModel, object entityModel, ManageController controller)
    {
        BoundModel = boundModel;
        EntityModel = entityModel;
        Form = controller.Request.Form;
    }

    public TabbableModel BoundModel { get; }
    public object EntityModel { get; }
    public IFormCollection Form { get; }
    public int StoreScope { get; }
}
