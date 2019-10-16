using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorRouting.Models
{
    public class SearchableTreeNode
    {
        // Name of this Node in the tree    
        public string Name { get; set; }

        //List of child items. These are expected to be other SearchableTreeNodes or SampleInfos
        public List<object> Items { get; set; }

        /// <summary>
        /// Creates a new SearchableTreeNode from a list of items
        /// </summary>
        /// <param name="name">The name of this node in the tree</param>
        /// <param name="items">A list containig <c>SampleInfo</c>s and <c>SearchableTreeNode</c>s.</param>
        public SearchableTreeNode(string name, IEnumerable<object> items)
        {
            Name = name;
            Items = items.ToList();
        }

        /// <summary>
        /// Searches the node and any sub-nodes for samples matching the predicate.
        /// </summary>
        /// <param name="predicate">Function that should return true for any matching samples.</param>
        /// <returns><c>Null</c> if there are no matches, a <c>SearchableTreeNode</c> if there are.</returns>
        public SearchableTreeNode Search(Func<SampleInfo, bool> predicate)
        {
            //Search recursively if this node contains sub-trees
            var subTrees = Items.OfType<SearchableTreeNode>()
                                .Select(cn => cn.Search(predicate))
                                .Where(cn => cn != null)
                                .ToArray();
            if(subTrees.Any())
            {
                return new SearchableTreeNode(Name, subTrees);
            }

            // If the node contains samples, search those
            var matchingSamples = Items.OfType<SampleInfo>()
                                       .Where(predicate)
                                       .ToArray();

            // Return null if there are no results
            return matchingSamples.Any() ? new SearchableTreeNode(Name, matchingSamples) : null;

        }
    }
}
