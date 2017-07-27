﻿using System;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;

namespace Umbraco.Web.PublishedCache
{
    internal abstract class PropertySetPropertyBase : PublishedPropertyBase
    {
        private readonly object _locko = new object();
        private readonly object _sourceValue;

        protected readonly IPropertySet Set;
        protected readonly bool IsPreviewing;
        protected readonly bool IsMember;

        private bool _interInitialized;
        private object _interValue;
        private CacheValues _cacheValues;

        // initializes a published item property
        protected PropertySetPropertyBase(PublishedPropertyType propertyType, IPropertySet set, bool previewing, PropertyCacheLevel referenceCacheLevel, object sourceValue = null)
            : base(propertyType, referenceCacheLevel)
        {
            _sourceValue = sourceValue;
            Set = set;
            IsPreviewing = previewing;
            IsMember = propertyType.ContentType.ItemType == PublishedItemType.Member;
        }

        public override bool HasValue => _sourceValue != null
            && (!(_sourceValue is string) || string.IsNullOrWhiteSpace((string) _sourceValue) == false);

        protected class CacheValues
        {
            public bool ObjectInitialized;
            public object ObjectValue;
            public bool XPathInitialized;
            public object XPathValue;
        }

        private void GetCacheLevels(out PropertyCacheLevel cacheLevel, out PropertyCacheLevel referenceCacheLevel)
        {
            // based upon the current reference cache level (ReferenceCacheLevel) and this property
            // cache level (PropertyType.CacheLevel), determines both the actual cache level for the
            // property, and the new reference cache level.

            // if the property cache level is 'shorter-termed' that the reference
            // then use it and it becomes the new reference, else use Content and
            // don't change the reference.
            //
            // examples:
            // currently (reference) caching at facade, property specifies
            // snapshot, ok to use content. OTOH, currently caching at snapshot,
            // property specifies facade, need to use facade.
            //
            if (PropertyType.CacheLevel > ReferenceCacheLevel || PropertyType.CacheLevel == PropertyCacheLevel.None)
            {
                cacheLevel = PropertyType.CacheLevel;
                referenceCacheLevel = cacheLevel;
            }
            else
            {
                cacheLevel = PropertyCacheLevel.Content;
                referenceCacheLevel = ReferenceCacheLevel;
            }
        }

        protected abstract CacheValues GetSnapshotCacheValues();
        protected abstract CacheValues GetFacadeCacheValues();

        private CacheValues GetCacheValues(PropertyCacheLevel cacheLevel)
        {
            CacheValues cacheValues;
            switch (cacheLevel)
            {
                case PropertyCacheLevel.None:
                    // never cache anything
                    cacheValues = new CacheValues();
                    break;
                case PropertyCacheLevel.Content:
                    // cache within the property object itself, ie within the content object
                    cacheValues = _cacheValues ?? (_cacheValues = new CacheValues());
                    break;
                case PropertyCacheLevel.Snapshot:
                    // cache within the snapshot cache, depending...
                    cacheValues = GetSnapshotCacheValues();
                    break;
                case PropertyCacheLevel.Facade:
                    // cache within the facade cache
                    cacheValues = GetFacadeCacheValues();
                    break;
                default:
                    throw new InvalidOperationException("Invalid cache level.");
            }
            return cacheValues;
        }

        private object GetInterValue()
        {
            if (_interInitialized) return _interValue;

            _interValue = PropertyType.ConvertSourceToInter(Set, _sourceValue, IsPreviewing);
            _interInitialized = true;
            return _interValue;
        }

        public override object SourceValue => _sourceValue;

        public override object Value
        {
            get
            {
                lock (_locko)
                {
                    GetCacheLevels(out PropertyCacheLevel cacheLevel, out PropertyCacheLevel referenceCacheLevel);

                    var cacheValues = GetCacheValues(cacheLevel);
                    if (cacheValues.ObjectInitialized) return cacheValues.ObjectValue;

                    cacheValues.ObjectValue = PropertyType.ConvertInterToObject(Set, referenceCacheLevel, GetInterValue(), IsPreviewing);
                    cacheValues.ObjectInitialized = true;
                    return cacheValues.ObjectValue;
                }
            }
        }

        public override object XPathValue
        {
            get
            {
                lock (_locko)
                {
                    GetCacheLevels(out PropertyCacheLevel cacheLevel, out PropertyCacheLevel referenceCacheLevel);

                    var cacheValues = GetCacheValues(cacheLevel);
                    if (cacheValues.XPathInitialized) return cacheValues.XPathValue;

                    cacheValues.XPathValue = PropertyType.ConvertInterToXPath(Set, referenceCacheLevel, GetInterValue(), IsPreviewing);
                    cacheValues.XPathInitialized = true;
                    return cacheValues.XPathValue;
                }
            }
        }
    }
}
