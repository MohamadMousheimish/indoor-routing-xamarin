using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;

namespace IndoorRouting
{
    internal static class IntentBroker
    {
        private static Dictionary<string, Dictionary<string, Queue<object>>> _brokeredObjects = new Dictionary<string, Dictionary<string, Queue<object>>>();

        /// <summary>
        /// Adds an arbitrary object to an intent for passing to an activity
        /// </summary>
        /// <remarks>
        /// This method and the <see cref="GetExtra"/> method allow passing arbitrary objects between activites.
        /// Note that the methods behave in a first-in-first-out (FIFO) manner, scoped to the name of the type
        /// for which PutExtra was called.  That is, if two PutExtra calls are made with the same type name, then
        /// the objects passed with those calls, when retrieved via calls to GetExtra, will come out in the sequence
        /// that they went in with.  It could therefore be possible that the objects are ultimately mapped incorrectly
        /// to the activities within which they are retrieved.
        /// </remarks>
        public static void PutExtra(this Intent intent, string intentName, object o)
        {
            var className = intent.Component.ClassName;
            if (!_brokeredObjects.ContainsKey(className))
                _brokeredObjects[className] = new Dictionary<string, Queue<object>>();

            if (!_brokeredObjects[className].ContainsKey(intentName))
                _brokeredObjects[className][intentName] = new Queue<object>();
            
            _brokeredObjects[className][intentName].Enqueue(o);
        }

        public static object GetExtra(this Activity activity, string intentName)
        {
            var className = activity.Class.Name;
            if (!_brokeredObjects.ContainsKey(className))
                return null;

            var objectsForType = _brokeredObjects[className];
            if (!objectsForType.ContainsKey(intentName))
                return null;

            return objectsForType[intentName].Count > 0 ? objectsForType[intentName].Dequeue() : null;
        }
    }
}
