using System.Collections.Generic;
using System.Linq;
using OWML.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NBodyChaos;

/**
 * COPIED FROM https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Utility/SearchUtilities.cs
 */
public static class SearchUtilities
    {
        private static readonly Dictionary<string, GameObject> CachedGameObjects = new Dictionary<string, GameObject>();

        public static void ClearCache()
        {
            NBodyChaos.Console.WriteLine("Clearing search cache");
            CachedGameObjects.Clear();
        }

        public static List<T> FindObjectsOfTypeAndName<T>(string name) where T : Object
        {
            T[] firstList = GameObject.FindObjectsOfType<T>();
            List<T> finalList = new List<T>();

            for (var i = 0; i < firstList.Length; i++)
            {
                if (firstList[i].name == name)
                {
                    finalList.Add(firstList[i]);
                }
            }

            return finalList;
        }

        public static T FindObjectOfTypeAndName<T>(string name) where T : Object
        {
            T[] firstList = GameObject.FindObjectsOfType<T>();

            for (var i = 0; i < firstList.Length; i++)
            {
                if (firstList[i].name == name)
                {
                    return firstList[i];
                }
            }

            return null;
        }

        public static List<T> FindResourcesOfTypeAndName<T>(string name) where T : Object
        {
            T[] firstList = Resources.FindObjectsOfTypeAll<T>();
            List<T> finalList = new List<T>();

            for (var i = 0; i < firstList.Length; i++)
            {
                if (firstList[i].name == name)
                {
                    finalList.Add(firstList[i]);
                }
            }

            return finalList;
        }

        public static T FindResourceOfTypeAndName<T>(string name) where T : Object
        {
            T[] firstList = Resources.FindObjectsOfTypeAll<T>();

            for (var i = 0; i < firstList.Length; i++)
            {
                if (firstList[i].name == name)
                {
                    return firstList[i];
                }
            }

            return null;
        }

        public static string GetPath(this Transform current)
        {
            if (current.parent == null) return current.name;
            return current.parent.GetPath() + "/" + current.name;
        }

        public static GameObject FindChild(this GameObject g, string childPath) =>
            g.transform.Find(childPath)?.gameObject;

        /// <summary>
        /// finds active or inactive object by path,
        /// or recursively finds an active or inactive object by name
        /// </summary>
        public static GameObject Find(string path, bool warn = true)
        {
            if (CachedGameObjects.TryGetValue(path, out var go) && go != null) return go;

            // 1: normal find
            go = GameObject.Find(path);
            if (go)
            {
                CachedGameObjects.Add(path, go);
                return go;
            }

            // 2: find inactive using root + transform.find
            var names = path.Split('/');

            var rootName = names[0];
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == rootName);

            var childPath = string.Join("/", names.Skip(1));
            go = root ? root.FindChild(childPath) : null;
            if (go)
            {
                CachedGameObjects.Add(path, go);
                return go;
            }

            var name = names.Last();
            if (warn) NBodyChaos.Console.WriteLine($"Couldn't find object in path {path}, will look for potential matches for name {name}", MessageType.Warning);
            // 3: find resource to include inactive objects (but skip prefabs)
            go = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(x => x.name == name && x.scene.name != null);
            if (go)
            {
                CachedGameObjects.Add(path, go);
                return go;
            }

            if (warn) NBodyChaos.Console.WriteLine($"Couldn't find object with name {name}", MessageType.Warning);
            return null;
        }

        public static List<GameObject> GetAllChildren(this GameObject parent)
        {
            var children = new List<GameObject>();
            foreach (Transform child in parent.transform)
            {
                children.Add(child.gameObject);
            }
            return children;
        }
    }