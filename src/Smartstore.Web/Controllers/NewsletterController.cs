using Smartstore.Core.Identity;
using Smartstore.Core.Localization.Routing;
using Smartstore.Core.Messaging;
using Smartstore.Core.Security;
using Smartstore.Core.Seo.Routing;
using Smartstore.Core.Stores;
using Smartstore.Web.Models.Newsletter;

namespace Smartstore.Web.Controllers
{
    public class NewsletterController : PublicController
    {
        private readonly SmartDbContext _db;
        private readonly IWorkContext _workContext;
        private readonly IStoreContext _storeContext;
        private readonly IMessageFactory _messageFactory;
        private readonly PrivacySettings _privacySettings;

        public NewsletterController(
            SmartDbContext db,
            IWorkContext workContext,
            IStoreContext storeContext,
            IMessageFactory messageFactory,
            PrivacySettings privacySettings)
        {
            _db = db;
            _workContext = workContext;
            _storeContext = storeContext;
            _messageFactory = messageFactory;
            _privacySettings = privacySettings;
        }

        [HttpPost]
        [GdprConsent, DisallowRobot, ValidateHoneypot]
        [LocalizedRoute("newsletter/subscribe", Name = "SubscribeNewsletter")]
        public async Task<IActionResult> Subscribe(bool subscribe, string email)
        {
            string result;
            var customer = _workContext.CurrentCustomer;
            var hasConsentedToGdpr = customer.GenericAttributes.HasConsentedToGdpr;
            var hasConsented = ViewData["GdprConsent"] != null ? (bool)ViewData["GdprConsent"] : hasConsentedToGdpr;

            if (!hasConsented && _privacySettings.DisplayGdprConsentOnForms)
            {
                return Json(new { Success = false, Result = string.Empty });
            }

            if (!email.IsEmail())
            {
                return Json(new { Success = false, Result = T("Newsletter.Email.Wrong").Value });
            }

            // Subscribe/unsubscribe.
            email = email.Trim();

            var store = _storeContext.CurrentStore;
            var language = _workContext.WorkingLanguage;
            var subscription = await _db.NewsletterSubscriptions
                .AsNoTracking()
                .ApplyMailAddressFilter(email, store.Id)
                .FirstOrDefaultAsync();

            if (subscription != null)
            {
                if (subscribe)
                {
                    if (!subscription.Active)
                    {
                        await _messageFactory.SendNewsletterSubscriptionActivationMessageAsync(subscription, language.Id);
                    }
                    result = T("Newsletter.SubscribeEmailSent");
                }
                else
                {
                    if (subscription.Active)
                    {
                        await _messageFactory.SendNewsletterSubscriptionDeactivationMessageAsync(subscription, language.Id);
                    }
                    result = T("Newsletter.UnsubscribeEmailSent");
                }
            }
            else if (subscribe)
            {
                subscription = new()
                {
                    NewsletterSubscriptionGuid = Guid.NewGuid(),
                    Email = email,
                    Active = false,
                    CreatedOnUtc = DateTime.UtcNow,
                    StoreId = store.Id,
                    WorkingLanguageId = language.Id
                };

                _db.NewsletterSubscriptions.Add(subscription);
                await _db.SaveChangesAsync();

                await _messageFactory.SendNewsletterSubscriptionActivationMessageAsync(subscription, language.Id);

                result = T("Newsletter.SubscribeEmailSent");
            }
            else
            {
                result = T("Newsletter.UnsubscribeEmailSent");
            }

            return Json(new { Success = true, Result = result });
        }

        [HttpGet]
        [DisallowRobot]
        [LocalizedRoute("/newsletter/subscriptionactivation/{token}/{active}", Name = "NewsletterActivation")]
        public async Task<IActionResult> SubscriptionActivation(Guid token, bool active)
        {
            var subscription = await _db.NewsletterSubscriptions.FirstOrDefaultAsync(x => x.NewsletterSubscriptionGuid == token);
            if (subscription == null)
            {
                return NotFound();
            }

            if (active)
            {
                subscription.Active = active;
            }
            else
            {
                _db.NewsletterSubscriptions.Remove(subscription);
            }

            await _db.SaveChangesAsync();

            var model = new SubscriptionActivationModel
            {
                Result = T(active ? "Newsletter.ResultActivated" : "Newsletter.ResultDeactivated")
            };

            return View(model);
        }
    }
}