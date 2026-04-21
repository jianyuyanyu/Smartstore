using Smartstore.Core.Identity;
using Smartstore.Core.Stores;

namespace Smartstore.Core.Messaging
{
    public partial interface INewsletterSubscriptionService
    {
        /// <summary>
        /// Subscribes the specified email address to the newsletter.
        /// </summary>
        /// <param name="email">The email address to subscribe. Cannot be <c>null</c> or empty.</param>
        /// <param name="customer">
        /// The customer associated with the email address.
        /// If <c>null</c>, customer will be obtained via <see cref="IWorkContext.CurrentCustomer"/>.
        /// </param>
        /// <param name="active">
        /// This value indicates whether the subscription should be activated immediately. 
        /// If <c>true</c>, the subscription is active and reward points will be applied (if enabled).
        /// Otherwise, a newsletter activation message is sent to confirm the subscription.
        /// </param>
        /// <param name="storeId">
        /// The identifier of the store for which the subscription applies.
        /// If <c>null</c>, store will be obtained via <see cref="IStoreContext.CurrentStore"/>.
        /// </param>
        /// <returns><c>true</c> if the subscription was successful. Otherwise <c>false</c>.</returns>
        Task<bool> SubscribeAsync(
            string email,
            Customer customer = null,
            bool active = false,
            int? storeId = null);

        /// <summary>
        /// Unsubscribes the specified email address from the newsletter subscription list.
        /// </summary>
        /// <param name="email">The email address to unsubscribe from the newsletter. Cannot be <c>null</c> or empty.</param>
        /// <param name="customer">
        /// The customer associated with the email address.
        /// If <c>null</c>, customer will be obtained via <see cref="IWorkContext.CurrentCustomer"/>.
        /// </param>
        /// <param name="remove">
        /// <c>true</c> to remove the subscription record entirely and to reduce reward points (if enabled).
        /// Otherwise, a newsletter deactivation message is sent to confirm the unsubscription.
        /// </param>
        /// <param name="storeId">
        /// The identifier of the store for which the subscription applies.
        /// If <c>null</c>, store will be obtained via <see cref="IStoreContext.CurrentStore"/>.
        /// </param>
        /// <returns><c>true</c> if the unsubscription was successful. Otherwise <c>false</c>.</returns>
        Task<bool> UnsubscribeAsync(
            string email,
            Customer customer = null,
            bool remove = true,
            int? storeId = null);

        /// <summary>
        /// Adds or deletes a newsletter subscription and sends newsletter activation message to subscriber in case of addition.
        /// The caller is responsible for database commit.
        /// </summary>
        /// <param name="subscribe"><c>true</c> adds subscription, <c>false</c> removes subscription</param>
        /// <returns><c>true</c> added subscription, <c>false</c> removed subscription, <c>null</c> did nothing</returns>
        Task<bool?> ApplySubscriptionAsync(bool subscribe, string email, int storeId);
    }
}
