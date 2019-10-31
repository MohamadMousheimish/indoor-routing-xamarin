using IndoorRouting.Attributes;
using IndoorRouting.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting.Managers
{
    /// <summary>
    /// Single instance class to manage samples
    /// </summary>
    public class SampleManager
    {
        // Private Constructor
        private SampleManager() { }

        // Static initialization of the unique instance
        private static readonly SampleManager SingleInstance = new SampleManager();

        public static SampleManager Current { get { return SingleInstance; } }

        /// <summary>
        /// A List of all Samples
        /// </summary>
        /// <remarks>This is public on purpose. Other solutions that consume
        /// this project reference it directly</remarks>
        public IList<SampleInfo> AllSamples { get; set; }

        /// <summary>
        /// A collection of all samples organized by category.
        /// </summary>
        public SearchableTreeNode FullTree { get; set; }

        /// <summary>
        /// The sample that is currently being shown to the user.
        /// </summary>
        public SampleInfo SelectedSample { get; set; }

        /// <summary>
        /// Initializes the sample manager by loading all of the samples in the app.
        /// </summary>
        public void Initialize()
        {
            // Get the currently-executing assembly.
            var samplesAssembly = GetType().GetTypeInfo().Assembly;

            // Get the list of all samples in the assembly
            AllSamples = CreateSampleInfos(samplesAssembly).OrderBy(info => info.Category)
                .ThenBy(info => info.SampleName.ToLowerInvariant())
                .ToList();

            // Create a tree from the list of all samples
            FullTree = BuildFullTree(AllSamples);
        }

        /// <summary>
        /// Create a list of sample metadata objects for each sample in the assembly
        /// </summary>
        /// <param name="assembly">The assembly to search for samples.</param>
        /// <returns>List of sample metadata objects.</returns>
        private static IList<SampleInfo> CreateSampleInfos(Assembly assembly)
        {
            // Get all the types in the assembly that are decorated with a SampleAttribute
            var sampleTypes = assembly.GetTypes()
                                      .Where(type => type.GetTypeInfo().GetCustomAttributes().OfType<SampleAttribute>().Any());

            // Create a list to hold all constructed sample metadata objects
            var samples = new List<SampleInfo>();

            // Create the sample metadata for each sample
            foreach (Type type in sampleTypes)
            {
                try
                {
                    samples.Add(new SampleInfo(type));
                }catch(Exception e)
                {
                    Debug.WriteLine($"Could not create sample from {type}: {e}");
                }
            }
            return samples;
        }

        /// <summary>
        /// Creates a <c>SearchableTreeNode</c> representing  the entire
        /// collection of samples, organized by category.
        /// </summary>
        /// <remarks>This is public on purpose. Other solutions that
        /// consume this project reference it directly.</remarks>
        /// <param name="allSamples">A list of all samples.</param>
        /// <returns>A <c>SearchableTreeNode</c> with all samples organized by category</returns>
        public static SearchableTreeNode BuildFullTree(IEnumerable<SampleInfo> allSamples)
        {
            // This code only supports one level of nesting
            return new SearchableTreeNode(
                "All Samples",
                allSamples.ToLookup(s => s.Category) // Put samples into lookup by category
                .OrderBy(s => s.Key)
                .Select(BuildTreeForCategory) // Create a tree for each category
                .ToList()
                );
        }

        /// <summary>
        /// Creates a <c>SearchableTreeNode</c> representing a category of samples
        /// </summary>
        /// <param name="byCategory">A grouping that associates one category title with many samples.</param>
        /// <returns>A <c>SearchableTreeNode</c> representing a category of samples.</returns>
        private static SearchableTreeNode BuildTreeForCategory(IGrouping<string, SampleInfo> byCategory)
        {
            // This code only supports one level of nesting
            return new SearchableTreeNode(
                name: byCategory.Key,
                items: byCategory.OrderBy(si => si.SampleName.ToLower()).ToList());
        }

    }
}
