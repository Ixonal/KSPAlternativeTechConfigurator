using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ATC
{
    public class RDNodeFactory
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

        //private class RDNodePrefab : RDNode
        //{
        //    public RDNodePrefab()
        //    {
        //        children = new List<RDNode>();
        //        controller = GameObject.FindObjectOfType<RDController>();
        //        parents = new Parent[0];
        //        treeNode = true;
        //        prefab = new GameObject();
        //        SetButtonState(State.HIDDEN);
        //        SetIconState(Icon.GENERIC);
        //        Setup();
        //    }

        //}

        #region Properties and Fields

        //we should only need one instance of this to create a new item (as one would with a prototype in Javascript)
        private static GameObject _nodePrefab;
        private static GameObject nodePrefab
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
                RDNode existingNode = AssetBase.RnDTechTree.GetTreeNodes().OrderBy(n => new System.Random().Next()).FirstOrDefault(n => n.controller != null);
                //RDNode existingNode = GameObject.FindObjectsOfType<RDNode>().FirstOrDefault(n => n.treeNode && n.controller != null);

                if (existingNode == null)
                {
                    Debug.Log("No existing node on KSP startup... that should never happen.");
                }

                Debug.Log("Existing node has controller: " + (existingNode.controller != null));
                Debug.Log("Existing node has graphics: " + (existingNode.graphics != null));

                Debug.Log("RDNodePrefab exists anywhere: " + (GameObject.FindObjectsOfType<RDNodePrefab>().Any()));

                Debug.Log("Existing node prefab name: " + existingNode.prefab.name);

                //In this case, we have to create a new prefab. Unfortunately, RDNode throws an argument null exception when the prefab tries to add it.
                //Since it seems to be able to recover after this, it must not be too much of an issue.
                _nodePrefab = new GameObject();
                //_nodePrefab.name = "DefaultNodePrefab";
                _nodePrefab.transform.parent = existingNode.transform.parent;

                Debug.Log("Creating RDNode...");
                RDNode nodePart = _nodePrefab.AddComponent("RDNode") as RDNode;
                //RDNode nodePart = _nodePrefab.GetComponent<RDNode>();
                nodePart.name = "newtech_rename";
                nodePart.description = "";
                nodePart.controller = existingNode.controller;
                nodePart.scale = existingNode.scale;
                nodePart.treeNode = true;
                nodePart.prefab = existingNode.prefab;
                nodePart.parents = new RDNode.Parent[0]; //don't want this to be null, but also don't want it to have null elements

                RDNodePrefab prefabPart = nodePart.graphics = _nodePrefab.AddComponent("RDNodePrefab") as RDNodePrefab;

                Debug.Log("nodePart exists: " + (nodePart != null));

                Debug.Log("Creating RDTech...");
                RDTech techPart = nodePart.tech = _nodePrefab.AddComponent("RDTech") as RDTech;
                techPart.state = RDTech.State.Available;
                techPart.techID = nodePart.name;
                techPart.partsAssigned = new List<AvailablePart>();
                techPart.partsPurchased = new List<AvailablePart>();
                //RDTech techPart = nodePart.tech = _nodePrefab.GetComponent<RDTech>();
                Debug.Log("techPart exists: " + (techPart != null));

                _nodePrefab.SetActive(false);

                return _nodePrefab;
            }
        }

        #endregion

        #region Public Interface

        public RDNode Create()
        {

            Debug.Log("Attempting to create a new Tech Node");

            //GameObject nodeObject = GameObject.Instantiate(nodePrefab) as GameObject;
            //RDNode node = nodeObject.GetComponent<RDNode>();
            //node.tech = nodeObject.GetComponent<RDTech>();
            //nodeObject.SetActive(true);

            RDNode node = GameObject.Instantiate(nodePrefab.GetComponent("RDNode")) as RDNode;
            node.tech = GameObject.Instantiate(nodePrefab.GetComponent("RDTech")) as RDTech;
            node.graphics = GameObject.Instantiate(nodePrefab.GetComponent("RDNodePrefab")) as RDNodePrefab;
            node.name = "newtech_rename";
            node.description = "";
            //node.Eviscerate();
            //node.graphics.Eviscerate();
            node.gameObject.SetActive(true);

            Debug.Log("new node is distinct from prefab: " + (!ReferenceEquals(node, nodePrefab.GetComponent("RDNode"))));
            Debug.Log("new node has an associated tech: " + (node.tech != null));
            Debug.Log("new node parents: " + node.parents);


            return node;
        }

        public RDNode Create(string techId)
        {
            RDNode node = Create();

            node.name = techId;

            return node;
        }

        #endregion

    }
}
