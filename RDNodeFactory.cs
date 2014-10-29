using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ATC
{
    public class RDNodeFactory : UnityEngine.Object
    {
        #region Singleton Instantiation

        private static RDNodeFactory _instance;
        public static RDNodeFactory Instance
        {
            get
            {
                return _instance ?? (_instance = new RDNodeFactory());
            }
        }

        #endregion

        #region .ctor

        //no external instantiation, that's what the Instance property is for
        private RDNodeFactory()
        {

        }

        #endregion

        #region Properties and Fields

        private static List<RDNode> _existingNodes = new List<RDNode>();

        private const string _newNodeName = "newnode_rename";
        private const string _newTechName = "newtech_rename";

        private static readonly System.Random _rng = new System.Random();


        //we should only need one instance of this to create a new item (as one would with a prototype in Javascript)
        private static GameObject _nodePrefab;
        private static GameObject NodePrefab
        {
            get
            {
                //If we've previously created a prefab, we can just use that
                if (_nodePrefab != null)
                {
                    Debug.Log("Node Prefab previously defined, using that.");
                    return _nodePrefab;
                }

                Debug.Log("Creating a default Node Prefab instance.");
                RDNode existingNode = GetRandomExistingNode();

                if (existingNode == null)
                {
                    throw new NullReferenceException("No existing node on KSP startup... that should never happen.");
                }

                Debug.Log("existingNode: " + existingNode);

                _nodePrefab = new GameObject(_newNodeName);
                _nodePrefab.transform.parent = existingNode.transform.parent;
                _nodePrefab.transform.localPosition = existingNode.transform.localPosition;
                _nodePrefab.SetActive(false);

                Debug.Log("Creating RDNode...");
                RDNode nodePart = _nodePrefab.AddComponent<RDNode>();
                nodePart.name = _newNodeName;
                nodePart.description = "";
                nodePart.controller = existingNode.controller;
                nodePart.scale = existingNode.scale;
                nodePart.prefab = existingNode.prefab;
                nodePart.icon = RDNode.Icon.GENERIC;
                nodePart.parents = new RDNode.Parent[0]; //don't want this to be null, but also don't want it to have null elements

                Debug.Log("nodePart exists: " + (nodePart != null));

                Debug.Log("Creating RDTech...");
                RDTech techPart = nodePart.tech = _nodePrefab.AddComponent<RDTech>();
                techPart.techID = _newTechName;
                Debug.Log("techPart exists: " + (techPart != null));


                return _nodePrefab;
            }
        }

        #endregion

        #region Private Methods

        private static RDNode GetRandomExistingNode()
        {
            return AssetBase.RnDTechTree.GetTreeNodes()
                                        .OrderBy(n => _rng.Next())
                                        .FirstOrDefault(n => n.treeNode);
        }

        #endregion

        #region Public Interface

        public void Init()
        {
            Debug.Log("Running Init()");

            _existingNodes.Clear();
            _existingNodes.AddRange(TechChanger.GetKnownNodes());

            if (_nodePrefab != null)
            {
                DestroyImmediate(_nodePrefab);
                _nodePrefab = null;
            }
            
        }

        public RDNode Create()
        {

            GameObject clone = GameObject.Instantiate(NodePrefab) as GameObject;
            RDNode node = clone.GetComponent<RDNode>();
            node.tech = clone.GetComponent<RDTech>();

            node.name = _newNodeName;
            node.children = new List<RDNode>();
            node.description = "";
            clone.transform.localPosition = NodePrefab.transform.localPosition;
            clone.transform.parent = NodePrefab.transform.parent;
            node.tech.Start();
            clone.SetActive(true);

            return node;
        }

        #endregion

    }
}
