using IndoorRouting.Attributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting.Models
{
    public class SampleInfo
    {

#if NETFX_CORE
        private string _pathStub = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
#else
        private string _pathStub = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#endif

        /// <summary>
        /// Get the path to the sample on disk
        /// </summary>
        public string Path { get { return System.IO.Path.Combine(_pathStub, "Activities", Category, FormalName); } }

        /// <summary>
        /// The human-readable name of the sample
        /// </summary>
        public string SampleName { get; set; }

        /// <summary>
        /// The name of the sample as it appears in code
        /// </summary>
        public string FormalName { get; set; }

        /// <summary>
        /// The human-readable category of the sample
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// The description of the sample
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The Instructions of the sample
        /// </summary>
        public string Instructions { get; set; }

        /// <summary>
        /// A list of offline data items that should be downloaded
        /// from ArcGIS Online prior to loading the sample. These
        /// should be expressed as portal item identifiers.
        /// </summary>
        public IEnumerable<string> OfflineDataItems { get; set; }

        public IEnumerable<string> Tags { get; set; }

        public IEnumerable<string> AndroidLayouts { get; set; }

        public IEnumerable<string> XamlLayouts { get; set; }

        public IEnumerable<string> ClassFiles { get; set; }

        /// <summary>
        /// A list of files used by the sample as embedded reosurces
        /// (e.g. PictureMarkerSymbols\pin_star_blue.png)
        /// </summary>
        public IEnumerable<string> EmbeddedResources { get; set; }

        /// <summary>
        /// The expected filename of the sample's image, without path.
        /// This is intened for use on windows.
        /// </summary>
        public string Image { get { return $"{FormalName}.jpg"; } }

        /// <summary>
        /// The underlying .NET type for this sample.
        /// Note: this is used by the sample viewer to
        /// construct samples at run time
        /// </summary>
        public Type SampleType { get; set; }

        /// <summary>
        /// The path to the sample image on disk; intended for use on windows.
        /// </summary>
        public string SampleImageName { get { return System.IO.Path.Combine(Path, Image); } }

        /// <summary>
        /// Base directory for the samples; defaults to executable directory
        /// </summary>
        public string PathStub { get { return _pathStub; } set { _pathStub = value; } }

        /// <summary>
        /// This constructor is used when the sample type is in the executing assembly.
        /// </summary>
        /// <param name="sampleType">The type for the sample object</param>
        public SampleInfo(Type sampleType)
        {
            SampleType = sampleType;
            FormalName = SampleType.Name;
            var typeInfo = sampleType.GetTypeInfo();
            var sampleAttr = GetAttribute<SampleAttribute>(typeInfo);
            if(sampleAttr == null)
            {
                throw new ArgumentException("Type must be decorated with 'Sample' attribute");
            }
            var offlineDataAttr = GetAttribute<OfflineDataAttribute>(typeInfo);
            var xamlAttr = GetAttribute<XamlFilesAttribute>(typeInfo);
            var androidAttr = GetAttribute<AndroidLayoutAttribute>(typeInfo);
            var classAttr = GetAttribute<ClassFilterAttribute>(typeInfo);
            var embeddedResourceAttr = GetAttribute<EmbeddedResourceAttribute>(typeInfo);

            Category = sampleAttr.Category;
            Description = sampleAttr.Description;
            Instructions = sampleAttr.Instructions;
            SampleName = sampleAttr.Name;
            Tags = sampleAttr.Tags;

            if(offlineDataAttr != null)
            {
                OfflineDataItems = offlineDataAttr.Items;
            }
            if(xamlAttr != null)
            {
                XamlLayouts = xamlAttr.Files;
            }
            if(androidAttr != null) 
            {
                AndroidLayouts = androidAttr.Files;
            }
            if(classAttr != null)
            {
                ClassFiles = classAttr.Files;
            }
            if(embeddedResourceAttr != null)
            {
                EmbeddedResources = embeddedResourceAttr.Files;
            }
        }

        /// <summary>
        /// Gets the attribute of the Type <typeparamref name="T"/>
        /// for a type described by <paramref name="typeInfo"/>
        /// </summary>
        /// <typeparam name="T">The type of the attribute object to return</typeparam>
        /// <param name="typeInfo">Describe the type that will be examined</param>
        /// <returns></returns>
        private static T GetAttribute<T>(MemberInfo typeInfo) where T : Attribute
        {
            return typeInfo.GetCustomAttributes(typeof(T)).SingleOrDefault() as T;
        }

    }
}
