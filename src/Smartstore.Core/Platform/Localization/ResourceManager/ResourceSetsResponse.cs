﻿namespace Smartstore.Core.Localization
{
    [Serializable]
    public class ResourceSetsResponse
    {
        public string Version { get; set; }
        public int ResourceCount { get; set; }

        public List<ResourceSet> Resources { get; set; } = [];
    }

    [Serializable]
    public class ResourceSet
    {
        public int Id { get; set; }
        public int? PreviousSetId { get; set; }

        public string Name { get; set; }
        public string Version { get; set; }
        public string Type { get; set; }
        public bool Published { get; set; }
        public string DownloadUrl { get; set; }
        public DateTime UpdatedOn { get; set; }
        public int DisplayOrder { get; set; }

        public int TranslatedCount { get; set; }
        public decimal TranslatedPercentage { get; set; }

        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int DeletedCount { get; set; }

        public ResourcesLanguage Language { get; set; }

        [Serializable]
        public class ResourcesLanguage
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Culture { get; set; }
            public string TwoLetterIsoCode { get; set; }
            public bool Rtl { get; set; }
        }
    }
}
