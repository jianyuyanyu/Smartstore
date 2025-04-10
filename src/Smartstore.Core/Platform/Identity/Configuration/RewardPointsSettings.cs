﻿using Smartstore.Core.Checkout.Orders;
using Smartstore.Core.Configuration;

namespace Smartstore.Core.Identity
{
    public class RewardPointsSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether Reward Points Program is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value of Reward Points exchange rate.
        /// </summary>
        public decimal ExchangeRate { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value whether to round down reward points.
        /// </summary>
        public bool RoundDownRewardPoints { get; set; }

        /// <summary>
        /// Gets or sets a number of points awarded for registration.
        /// </summary>
        public int PointsForRegistration { get; set; }

        /// <summary>
        /// Gets or sets a number of points awarded for a product review.
        /// </summary>
        public int PointsForProductReview { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show reward points (including amount) to receive for a product review on product detail page.
        /// </summary>
        public bool ShowPointsForProductReview { get; set; }

        /// <summary>
        /// Gets or sets a number of points awarded for purchases (amount in primary store currency).
        /// </summary>
        public decimal PointsForPurchases_Amount { get; set; } = 10;

        /// <summary>
        /// Gets or sets a number of points awarded for purchases.
        /// </summary>
        public int PointsForPurchases_Points { get; set; } = 1;

        /// <summary>
        /// Gets or sets a value indicating whether to truncate the decimal places of the amount calculated for reward points awarded for a purchase.
        /// </summary>
        public bool RoundDownPointsForPurchasedAmount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show reward points (including amount) to receive for purchasing a product on product detail page.
        /// </summary>
        public bool ShowPointsForProductPurchase { get; set; }

        /// <summary>
        /// Points are awarded when the order status is.
        /// </summary>
        public OrderStatus PointsForPurchases_Awarded { get; set; } = OrderStatus.Complete;

        /// <summary>
        /// Points are canceled when the order is.
        /// </summary>
        public OrderStatus PointsForPurchases_Canceled { get; set; } = OrderStatus.Cancelled;
    }
}