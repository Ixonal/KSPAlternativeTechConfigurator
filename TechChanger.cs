using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using UnityEngine;


namespace ATC
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class TechChanger : MonoBehaviour
    {
        bool loadOnNextUpdate = false;

        static bool bIsInstantiated = false;
        static bool bRemoveEventsOnDestroy = true;

        ConfigNode settings = new ConfigNode();

        string debugCombo = "^D";
        string reloadCombo = "^R";
        string createNewCombo = "^N";

        private List<RDNode.Parent> parentConnectionsAlreadyProcessed = new List<RDNode.Parent>();

        void Start()
        {


            if (!bIsInstantiated)
            {
                GameEvents.onGUIRnDComplexSpawn.Add(new EventVoid.OnEvent(OnGUIRnDComplexSpawn));
                GameEvents.onGUIRnDComplexDespawn.Add(new EventVoid.OnEvent(OnGUIRnDComplexDespawn));
                GameEvents.onGameSceneLoadRequested.Add(new EventData<GameScenes>.OnEvent(OnGameSceneLoadRequested));
                GameEvents.OnTechnologyResearched.Add(new EventData<GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>>.OnEvent(OnTechnologyResearched));
                DontDestroyOnLoad(this);

                bIsInstantiated = true;
            }
            else
            {
                bRemoveEventsOnDestroy = false;

                Destroy(this);
            }
        }

        void OnDestroy()
        {
            if (bRemoveEventsOnDestroy)
            {
                GameEvents.onGUIRnDComplexSpawn.Remove(new EventVoid.OnEvent(OnGUIRnDComplexSpawn));
                GameEvents.onGUIRnDComplexDespawn.Remove(new EventVoid.OnEvent(OnGUIRnDComplexDespawn));
                
            }

            bRemoveEventsOnDestroy = true;
        }

        public void OnGUI()
        {            
            if (Event.current.Equals(Event.KeyboardEvent(debugCombo)))
            {
                Debug.Log("-------ATC Debug Dump triggered-----------------");
                debugDump();
            }

            if (!loadOnNextUpdate && Event.current.Equals(Event.KeyboardEvent(reloadCombo)))
            {
                Debug.Log("-------ATC Reloading Tree triggered-----------------");
                loadOnNextUpdate = true;     
            }

            if (!loadOnNextUpdate && Event.current.Equals(Event.KeyboardEvent(createNewCombo)))
            {
                Debug.Log("-------ATC attempting to create new node-----------------");
                RDNode node = RDNodeFactory.Instance.Create("TestNode");
            }
        }

        public void Update()
        {

            if (loadOnNextUpdate)
            {
                try
                {
                    LoadTree();
                }
                catch (Exception ex)
                {
                    Debug.Log("ATC: Error Loading tree - " + ex.Message + " at " + ex.StackTrace);
                }
                loadOnNextUpdate = false;
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                foreach (RDNode rdNode in AssetBase.RnDTechTree.GetTreeNodes())
                {
                    Debug.Log("updating graphics for " + rdNode.gameObject.name);
                    if (rdNode.state != RDNode.State.HIDDEN)
                        rdNode.UpdateGraphics(); //this also calls "SetButtonState", which calls Setup()
                }
            }

        }

        void OnGUIRnDComplexSpawn()
        {

        }

        void OnGUIRnDComplexDespawn()
        {
        }

        void OnTechnologyResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> evt)
        {

        }

        void OnGameSceneLoadRequested(GameScenes scene)
        {
            if (scene == GameScenes.SPACECENTER)
            {
                Debug.Log("ATC: entered spacecenter - loading tree");
                loadOnNextUpdate = true;
            }
        }
        private RDNode findStartNode()
        { 
            return Array.Find<RDNode> (AssetBase.RnDTechTree.GetTreeNodes(), x => x.gameObject.name == "node0_start");
        }

        private void LoadTree()
        {
            if (!ATCTreeDumper.m_bHasTreeAlreadyBeenDumped && ATCTreeDumper.m_bIsEnabled)
            {
                ATCTreeDumper.DumpCurrentTreeToFile( "StockTree.cfg", "stock" );
            }

            settings = getActiveSettingCfg();

            if (!settings.HasData)
            {
                return;
            }

            debugCombo = settings.GetValue("debugDumpKeyCombo");
            reloadCombo = settings.GetValue("reloadKeyCombo");

            parentConnectionsAlreadyProcessed.Clear();

            foreach (ConfigNode activeTreeCfg in settings.GetNodes("ACTIVE_TECH_TREE"))
            {
                ConfigNode tree = getTreeCfgForActiveTreeCfg(activeTreeCfg);

                if (!tree.HasData)
                {
                    Debug.LogError("TechChanger: Treeconfig '" + activeTreeCfg.GetValue("name") + "' empty/not found!");
                    continue;
                }

                setupBodyScienceParamsForTree( tree );

                Debug.Log("ATC: processing all TECH_NODE items");
                //check modify-nodes
                foreach (ConfigNode cfgNodeModify in tree.GetNodes("TECH_NODE"))
                {
                    string gameObjectName = cfgNodeModify.GetValue("name");
                    //Debug.Log("processing MODIFY_NODE " + gameObjectName);
                    RDNode treeNode = Array.Find<RDNode>(AssetBase.RnDTechTree.GetTreeNodes(), x => x.gameObject.name == gameObjectName);

                    if (treeNode.treeNode)
                    {
                        updateNode(treeNode, cfgNodeModify);
                    }
                    else
                    {
                        Debug.LogWarning("Could not find RDNode with gameObjectName == " + gameObjectName);
                    }

                }//end for all nodes;


                //deactivated for now
                processNewNodes(tree);
                


            }//end foreach tree-config


            List<RDNode> topoSortedNodes = calculateTopologicalSorting();
            foreach (RDNode rdNode in topoSortedNodes)
            {
                //Debug.Log("setting up anchors for " + rdNode.gameObject.name);

                for (int i = 0; i < rdNode.parents.Count(); ++i)
                {
                    if (parentConnectionsAlreadyProcessed.Contains(rdNode.parents[i]))
                    {
                        Debug.Log("Skipping auto-anchor assignment for node " + rdNode.gameObject.name);                            
                    }
                    else {
                        setupAnchors(rdNode, ref rdNode.parents[i]);
                    }

                    //warn for anchors that cannot be displayed properly. This might happen if a user-config overrides the auto-assignment. Or if the auto-assignment screws up
                    if (rdNode.parents[i].parent.anchor == RDNode.Anchor.BOTTOM)
                        Debug.LogWarning("ATC: Warning: Arrow from "+ rdNode.parents[i].parent.node.gameObject.name +"to"+ rdNode.gameObject.name +" will cannot be displayed because it uses parent anchor BOTTOM!");
                    if (rdNode.parents[i].anchor == RDNode.Anchor.TOP)
                        Debug.LogWarning("ATC: Warning: Arrow from " + rdNode.parents[i].parent.node.gameObject.name + "to" + rdNode.gameObject.name + " will cannot be displayed because it uses anchor TOP!"); 
                }
            }          
        }

        private void processNewNodes(ConfigNode tree)
        {
            Debug.Log("processing all NEW_NODE items"); 
            //RDController rdControl = GameObject.FindObjectOfType<RDController>();
            List<RDNode> newRDNodes = new List<RDNode>();

            try
            {
                foreach (ConfigNode cfgNodeNew in tree.GetNodes("NEW_NODE"))
                {
                    //only create RDNodes that are not yet in the Techtree
                    string newName = cfgNodeNew.GetValue("gameObjectName");
                    if (!Array.Exists<RDNode>(AssetBase.RnDTechTree.GetTreeNodes(), x => x.gameObject.name == newName))
                    {

                        Debug.Log("Number of tech entries before creating a new node: " + AssetBase.RnDTechTree.GetTreeNodes().Count());

                        RDNode newNode = RDNodeFactory.Instance.Create(); //createNode();

                        Debug.Log("Number of tech entries after creating a new node: " + AssetBase.RnDTechTree.GetTreeNodes().Count());

                        if (newNode.tech == null)
                            Debug.Log("newNode.tech is null after createNode");

                        //if (newNode.gameObject == null)
                        //    Debug.Log("newNode.gameObject is null after c-tor");

                        //Debug.Log("calling rdNode.Warmup()");
                        //newNode.Warmup(newTech);
                        //Debug.Log("NEWNODE: after Setup(), startNode has " + findStartNode().children.Count() + " children");

                        Debug.Log("New node's gameobject is active: " + (newNode.gameObject.activeSelf));
                        Debug.Log("New node's gameobject is active in heirarchy: " + (newNode.gameObject.activeInHierarchy));

                        Debug.Log("created node and tech, now setting basic tech parameters");
                        //setup all the basic parameters that are not handled in updatenode
                        newNode.treeNode = true;
                        newNode.gameObject.name = newName;
                        newNode.name = newName.Substring(newName.IndexOf("_"));
                        newNode.tech.techID = newNode.name;
                        newNode.SetButtonState(RDNode.State.RESEARCHABLE);

                        newNode.tech.hideIfNoParts = false;

                        if (ResearchAndDevelopment.Instance != null)
                        {
                            ProtoTechNode techProto = ResearchAndDevelopment.Instance.GetTechState(newNode.tech.techID) ?? new ProtoTechNode
                            {
                                partsPurchased = newNode.tech.partsPurchased,
                                techID = newNode.tech.techID
                            };
                            newNode.tech.state = techProto.state;
                            techProto.UpdateFromTechNode(newNode.tech);
                            ResearchAndDevelopment.Instance.SetTechState(newName, techProto);
                        }

                        Debug.Log("updating node with cfgFile-parameters");
                        updateNode(newNode, cfgNodeNew);


                        Debug.Log("number of node parents: " + (newNode.parents.Length));

                        Debug.Log("now warming up tech");
                        newNode.Warmup(newNode.tech);

                        Debug.Log("calling newnode.setup");
                        newNode.Setup(); //This sets the anchor offsets

                        if (newNode.gameObject == null)
                            Debug.Log("newNode.gameObject is still null after Setup");



                        Debug.Log("created new RDNode " + newNode.gameObject.name + " with RDTech.title=" + newNode.tech.title + "(techId) =" + newNode.tech.techID);
                        //Debug.Log("NEWNODE: after updateNode(), startNode has " + findStartNode().children.Count() + " children");

                        Debug.Log("Tech tree already contains new node: " + (AssetBase.RnDTechTree.GetTreeNodes().Contains(newNode)));

                        
                        //object rdControl = AssetBase.g;
                        Debug.Log("NEWNODE: calling RegisterNode(), AssetBase.TechTree has  " + AssetBase.RnDTechTree.GetTreeNodes().Count() + " entries");


                        //AssetBase.RnDTechTree.
                        //Debug.Log("RDController instance ID: " + newNode.controller.GetInstanceID());
                        //object rdController = 
                        //Debug.Log("RDController exists: " + (rdController != null));
                        newNode.controller.RegisterNode(newNode);
                        //newNode.controller.UpdatePanel();
                        //rdControl.RegisterNode(newNode); //TODO maybe this needs to be done the other way around?
                        Debug.Log("NEWNODE: after RegisterNode(), AssetBase.TechTree has  " + AssetBase.RnDTechTree.GetTreeNodes().Count() + " entries");

                        newNode.Eviscerate();
                        newNode.Start();
                        newNode.UpdateGraphics();

                        //Debug.Log("Invoking rdController.registerNode");
                        //typeof(RDNode).GetMethod("InitializeArrows", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(newNode, new object[] { });

                        newRDNodes.Add(newNode);

                        //addedNewNodes.Add(newName);
                    }//endif tech not yet added
                }//endof all newnodes


                //AARRGG this is readonly, cannot add to this....

                //Array.Resize<RDNode>(ref AssetBase.RnDTechTree.GetTreeNodes(), 60);
                //AssetBase.RnDTechTree.GetTreeNodes().Concat(newRDNodes); 
                //AssetBase.RnDTechTree.GetTreeTechs().Concat(newRDTechs);
            }
            catch (Exception ex)
            {
                Debug.LogError("Exception in NEWNODE processing - " + ex.Message + " at " + ex.StackTrace);
            }
        } //end loadTree()


        private void updateNode(RDNode treeNode, ConfigNode cfgNode)
        {
            if (cfgNode.HasValue("title"))
            {
                //treeNode.name = cfgNode.GetValue("title");
                treeNode.tech.title = cfgNode.GetValue("title");
            }

            if (cfgNode.HasValue("description"))
            {
                treeNode.description = cfgNode.GetValue("description").Replace( "\\n", "\n" );
                treeNode.tech.description = treeNode.description;
            }


            if (cfgNode.HasValue("scienceCost"))
                treeNode.tech.scienceCost = int.Parse(cfgNode.GetValue("scienceCost"));

            //Debug.Log("checking icon");
            if (cfgNode.HasValue("icon"))
            {
                //bool success = Enum.TryParse<RDNode.Icon>(cfgNode.GetValue("icon"), out icon); //.NET >= 4.0
                try
                {
                    RDNode.Icon icon = (RDNode.Icon)Enum.Parse(typeof(RDNode.Icon), cfgNode.GetValue("icon"));
                    treeNode.icon = icon;
                    //Debug.Log("Setting iconstate");
                    //treeNode.SetIconState(icon); //not required, game handles this automatically for stocknodes
                }
                catch (Exception ex)
                {
                    Debug.LogError("Invalid Icon name '" + cfgNode.GetValue("icon") + "'" + ex.Message);
                }

            }


            if (cfgNode.HasValue("anyParentUnlocks"))
                treeNode.AnyParentToUnlock = bool.Parse(cfgNode.GetValue("anyParentUnlocks"));

            if (cfgNode.HasValue("hideIfNoparts"))
            {
                treeNode.tech.hideIfNoParts = bool.Parse(cfgNode.GetValue("hideIfNoparts"));            
            }
            else
            {
                treeNode.tech.hideIfNoParts = false;
            }

            //setup parent/child relations
            updateParentsForNode(treeNode, cfgNode);

            if (cfgNode.HasValue("posX") || cfgNode.HasValue("posY"))
            {
                Vector3 newPos = treeNode.transform.localPosition;

                if (cfgNode.HasValue("posX"))
                    newPos.x = float.Parse(cfgNode.GetValue("posX"));
                if (cfgNode.HasValue("posY"))
                    newPos.y = float.Parse(cfgNode.GetValue("posY"));

                treeNode.transform.localPosition = newPos;
            }          

        }

        private List<RDNode> calculateTopologicalSorting()
        {
            List<RDNode> sortedList = new List<RDNode>();
            HashSet<RDNode> tempMarkedNodes = new HashSet<RDNode>();
            HashSet<RDNode> markedNodes = new HashSet<RDNode>();

            HashSet<RDNode> unmarkedNodes = new HashSet<RDNode>();
            foreach (RDNode rdNode in AssetBase.RnDTechTree.GetTreeNodes())
            {
                unmarkedNodes.Add(rdNode);
            }


            while (unmarkedNodes.Count > 0)
            {
                RDNode n = unmarkedNodes.First();
                visitNode(n, ref sortedList, ref unmarkedNodes, ref markedNodes, ref tempMarkedNodes);
            }

            return sortedList;
        }

        private void visitNode(RDNode rdNode, ref List<RDNode> sortedList, ref HashSet<RDNode> unmarkedNodes, ref HashSet<RDNode> markedNodes, ref HashSet<RDNode> tempMarkedNodes)
        {
            //Debug.Log("TOPOSORT visiting " + rdNode.gameObject.name);
            if (tempMarkedNodes.Contains(rdNode))
            {
                throw new Exception("ATC: Circular dependency in Tech-Node Graph! RDNode " + rdNode.gameObject.name + " is in a circular dependency with one of its direct or indirect parents");
            }

            if (!markedNodes.Contains(rdNode)) //this node has not been visited yet
            {
                tempMarkedNodes.Add(rdNode);

                //DFS-search recursive for all children
                foreach (RDNode child in rdNode.children)
                    visitNode(child, ref sortedList, ref unmarkedNodes, ref markedNodes, ref tempMarkedNodes);

                //Debug.Log("TOPOSORT marking node " + rdNode.gameObject.name);
                markedNodes.Add(rdNode);
                tempMarkedNodes.Remove(rdNode);

                unmarkedNodes.Remove(rdNode);

                sortedList.Insert(0, rdNode);//prepend to list
            }
        }

        private bool isAnchorAvailableForOutgoingArrows(RDNode node, RDNode.Anchor anchor)
        {
            foreach (RDNode.Parent incomingConnection in node.parents)
                if (incomingConnection.anchor == anchor)
                    return false;

            return true;
        }

        private void setupAnchors(RDNode target, ref RDNode.Parent connection)
        {
            RDNode source = connection.parent.node;
            
            //find main direction from outgoing node (parent) to target (connectionOwner) node to set anchor tags
            //Exception: Cannot display incoming and outgoing nodes on the same anchor
            Vector3 connectionVec = target.transform.localPosition - source.transform.localPosition;
    
            //calculate/setup anchors
            List<RDNode.Anchor> possibleParentAnchors = new List<RDNode.Anchor>();
            List<RDNode.Anchor> possibleTargetAnchors = new List<RDNode.Anchor>();

            if (connectionVec.x >= 0)
            {//left to right
                if (isAnchorAvailableForOutgoingArrows(source, RDNode.Anchor.RIGHT))
                    possibleParentAnchors.Add(RDNode.Anchor.RIGHT);
                possibleTargetAnchors.Add(RDNode.Anchor.LEFT);
            }
            else
            {
                if (isAnchorAvailableForOutgoingArrows(source, RDNode.Anchor.LEFT))
                    possibleParentAnchors.Add(RDNode.Anchor.LEFT);
                possibleTargetAnchors.Add(RDNode.Anchor.RIGHT);
            }

            if (connectionVec.y >= 0) //up
            {
                if (isAnchorAvailableForOutgoingArrows(source, RDNode.Anchor.TOP))
                    possibleParentAnchors.Add(RDNode.Anchor.TOP);
                possibleTargetAnchors.Add(RDNode.Anchor.BOTTOM);
            }
            else // TOP-DOWN connection doesnt work because of parent or target anchor 
            {
                //neither does or BOTTOM->LEFT  RIGHT->TOP neiter
                //possibleParentAnchors.Add(RDNode.Anchor.BOTTOM);
                //possibleTargetAnchors.Add(RDNode.Anchor.TOP);
            }
            

            //Debug.Log("options remaining after filtering: " + possibleParentAnchors.Count());
            //foreach (RDNode.Anchor anchor in possibleParentAnchors)
            //    Debug.Log("available anchor: " + anchor);

            //if two options are available, pick the larger distance
            if (Math.Abs(connectionVec.x) < Math.Abs(connectionVec.y)) //preferrably vertical            {
            {    
                possibleParentAnchors.Reverse();
                possibleTargetAnchors.Reverse();
            }

            if (possibleParentAnchors.Count == 0 || possibleTargetAnchors.Count == 0)
            {
                Debug.LogWarning("no valid anchor for connection " + source.gameObject.name + "->" + target.gameObject.name + ", direction = " + connectionVec.ToString() + ", defaulting to anchors RIGHT->LEFT");
                if (possibleParentAnchors.Count == 0)
                    possibleParentAnchors.Add(RDNode.Anchor.RIGHT);
                if (possibleTargetAnchors.Count == 0)
                    possibleTargetAnchors.Add(RDNode.Anchor.LEFT);
            }



            //Debug.Log("            anchors for connection " + source.gameObject.name + "->" + target.gameObject.name+ ", direction = " + connectionVec.ToString() + " anchors : " + possibleParentAnchors.First() + " -> " + possibleTargetAnchors.First());
        
            connection.anchor = possibleTargetAnchors.First();
            connection.parent.anchor = possibleParentAnchors.First();
        }

        [Obsolete("Functionality has been moved to the RDNodeFactory class")]
        private RDNode createNode()
        {
            //Debug.Log("creating new RDNode");

            RDNode startNode = findStartNode();

            GameObject nodePrefab;
            //if (startNode.prefab == null)
            //{
                Debug.Log("creating new GameObject()");
                nodePrefab = new GameObject("newnode", typeof(RDNode), typeof(RDTech));

                if (nodePrefab.GetComponent<RDNode>() == null)
                    Debug.Log ("wtf - nodePrefab.getComponent<RDNode> is null");
                if (nodePrefab.GetComponent<RDTech>() == null)
                    Debug.Log ("wtf - nodePrefab.getComponent<RDTech> is null");

                RDTech prefabTech = nodePrefab.GetComponent<RDTech>();
                nodePrefab.GetComponent<RDTech>().techID = "newTech_RenameMe";
                nodePrefab.GetComponent<RDNode>().tech = nodePrefab.GetComponent<RDTech>();
                nodePrefab.GetComponent<RDNode>().prefab = startNode.prefab;
                nodePrefab.GetComponent<RDNode>().parents = new RDNode.Parent[0];
                nodePrefab.GetComponent<RDNode>().icon = startNode.icon;
                nodePrefab.GetComponent<RDNode>().controller = startNode.controller;
                nodePrefab.GetComponent<RDNode>().scale = startNode.scale;
                nodePrefab.SetActive(false);
            //}
            //else
            //{
            //    nodePrefab = startNode.prefab;
            //}


            Debug.Log("Instantiating prefab nodeObject");

            GameObject clone = (GameObject)GameObject.Instantiate(nodePrefab);
            clone.SetActive(true);
            clone.transform.parent = startNode.transform.parent;
            clone.transform.localPosition = new Vector3(0, 50, 0);
            clone.GetComponent<RDNode>().children = new List<RDNode>();


            clone.GetComponent<RDNode>().tech = new RDTech();
            return clone.GetComponent<RDNode>();               
        
        }

        //draws arrows manually (not required atm)

        private void recreateArrows(RDNode rdNode)
        {
            if (rdNode.state != RDNode.State.HIDDEN)
            {
                for (int i = 0; i < rdNode.parents.Count(); ++i)
                {
                    //Recreate Parent hopefully recreates incoming array? nope, doesnt, also not with calling UpdateGraphics and/or Setup not...
                    //just changing the line does not update the graphics either.
                    RDNode.Parent parentStruct = rdNode.parents[i];
                    if (parentStruct.line != null)
                        Vector.DestroyLine(ref parentStruct.line);
                    if (parentStruct.arrowHead != null)
                        GameObject.Destroy(parentStruct.arrowHead);
                }//endfor foreach parentnode

                RDGridArea gridArea = GameObject.FindObjectOfType<RDGridArea>();
                //typeof(RDNode).GetMethod("InitializeArrows", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(treeNode, new object[] { });
                if (rdNode.state == RDNode.State.RESEARCHED || rdNode.state == RDNode.State.RESEARCHABLE)
                    typeof(RDNode).GetMethod("DrawArrow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(rdNode, new object[] { gridArea.LineMaterial });
                else
                    typeof(RDNode).GetMethod("DrawArrow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(rdNode, new object[] { gridArea.LineMaterialGray });
            }
        }

        private void setupBodyScienceParamsForTree(ConfigNode tree)
        {
            Debug.Log("ATC: setupBodyScienceParams()");
            //foreach (string treeName in tree.GetValues("TechTree"))
            {
                //Debug.Log("ATC: Loading tree " + treeName);

                foreach (ConfigNode scienceParamsNode in tree.GetNodes("BODY_SCIENCE_PARAMS"))
                {
                    
                    string bodyName = scienceParamsNode.GetValue("name");
                    //Debug.Log("Processing scienceParams for " + bodyName);

                    CelestialBody body = FlightGlobals.Bodies.Find(x => x.name == bodyName);
                    //Debug.Log("found matching body " + body.
                    try
                    {
                        Debug.Log("ATC: Modifying celestialBody science params for " + bodyName);
                        //Science value factors
                        if (scienceParamsNode.HasValue("LandedDataValue"))
                            body.scienceValues.LandedDataValue = float.Parse(scienceParamsNode.GetValue("LandedDataValue"));
                        if (scienceParamsNode.HasValue("SplashedDataValue"))
                            body.scienceValues.SplashedDataValue = float.Parse(scienceParamsNode.GetValue("SplashedDataValue"));
                        if (scienceParamsNode.HasValue("FlyingLowDataValue"))
                            body.scienceValues.FlyingLowDataValue = float.Parse(scienceParamsNode.GetValue("FlyingLowDataValue"));
                        if (scienceParamsNode.HasValue("FlyingHighDataValue"))
                            body.scienceValues.FlyingHighDataValue = float.Parse(scienceParamsNode.GetValue("FlyingHighDataValue"));
                        if (scienceParamsNode.HasValue("InSpaceLowDataValue"))
                            body.scienceValues.InSpaceLowDataValue = float.Parse(scienceParamsNode.GetValue("InSpaceLowDataValue"));
                        if (scienceParamsNode.HasValue("InSpaceHighDataValue"))
                            body.scienceValues.InSpaceHighDataValue = float.Parse(scienceParamsNode.GetValue("InSpaceHighDataValue"));
                        if (scienceParamsNode.HasValue("RecoveryValue"))
                            body.scienceValues.RecoveryValue = float.Parse(scienceParamsNode.GetValue("RecoveryValue"));

                        //Zone thresholds

                        if (scienceParamsNode.HasValue("flyingAltitudeThreshold"))
                            body.scienceValues.flyingAltitudeThreshold = float.Parse(scienceParamsNode.GetValue("flyingAltitudeThreshold"));
                        if (scienceParamsNode.HasValue("FlyingHighDataValue"))
                            body.scienceValues.spaceAltitudeThreshold = float.Parse(scienceParamsNode.GetValue("spaceAltitudeThreshold"));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }//endfor each celestialbody-string
            }//endfor each tree-configfile

        }

        private string internalizeName(string partName)
        {
            return partName.Replace("_", ".");
        }

        private void debugDump()
        { 
            //All directly accessible RDNodes
            Debug.Log("---------- DEBUGDUMP -------------");
            try
            {
                Debug.Log("RDNodes in GameObject.find() = " + GameObject.FindObjectsOfType<RDNode>().Count() + " - AssetBase.RnDTechtree has " + AssetBase.RnDTechTree.GetTreeNodes().Count() + " nodes");
                foreach (RDNode rdNode in GameObject.FindObjectsOfType<RDNode>())
                {
                    Debug.Log("RDNode " + rdNode.gameObject.name + " (" + rdNode.state + ") with tech " + rdNode.tech.title + ", #children " + rdNode.children.Count());
                    foreach (RDNode child in rdNode.children)
                        Debug.Log("   child: " + child.gameObject.name + "(" + child.tech.title + ")");
                }
            } catch (Exception)
            { }

            Debug.Log("...");
            Debug.Log("...");
            try{
                Debug.Log("RDNodes in AssetBase.rdTechTree " + AssetBase.RnDTechTree.GetTreeNodes().Count());
                foreach (RDNode rdNode in AssetBase.RnDTechTree.GetTreeNodes())
                {
                    Debug.Log("RDNode " + rdNode.gameObject.name + " (" + rdNode.state + ") with tech " + rdNode.tech.title + "(id=" +rdNode.tech.techID +"), #children " + rdNode.children.Count() + ", active = " + (rdNode.gameObject.activeSelf ? "true" : "false") + " partsAssigned = " + rdNode.PartsInTotal());
                }
                Debug.Log("...");
                Debug.Log("...");
            } catch (Exception)
            { }


            
        }

        private ConfigNode getActiveSettingCfg()
        {
            // this can be expanded upon in the future so that a player-specific custom settings file will override the default one

            return Array.Find<ConfigNode>(GameDatabase.Instance.GetConfigNodes("ATC_SETTINGS"), tempConfigNode => tempConfigNode.HasValue("name") && tempConfigNode.GetValue("name") == "default");
        }

        private ConfigNode getTreeCfgForActiveTreeCfg(ConfigNode activeTreeCfg)
        {
            if ( activeTreeCfg.HasValue("name"))
            {
                string treeName = activeTreeCfg.GetValue("name");

                return Array.Find<ConfigNode>(GameDatabase.Instance.GetConfigNodes("TECH_TREE"), 
                    tempTreeConfigNode => tempTreeConfigNode.HasValue("name") && tempTreeConfigNode.GetValue("name") == treeName);            
            }
            else
            {
                // return an empty config node to indicate failure, same as Find()

                return new ConfigNode();
            }
        }

        private void updateParentsForNode(RDNode treeNode, ConfigNode treeCfg)
        {
            //clear all old parents. The RD-Scene will take care of drawing the arrows
            clearParentsFromNode( treeNode );

            List<RDNode.Parent> connectionList = new List<RDNode.Parent>();

            foreach (ConfigNode parentCfg in treeCfg.GetNodes("PARENT_NODE"))
            {
                if (parentCfg.HasValue("name"))
                {
                    string parentName = parentCfg.GetValue("name");

                    RDNode parentNode = Array.Find<RDNode>(AssetBase.RnDTechTree.GetTreeNodes(), x => x.gameObject.name == parentName);

                    if (parentNode.gameObject.name == parentName)
                    {
                        parentNode.children.Add(treeNode);

                        RDNode.Parent connection;

                        // only manually override the anchor points if BOTH are specified in the config
                        if (parentCfg.HasValue("parentSide") && parentCfg.HasValue("childSide"))
                        {
                            
                            RDNode.Anchor parentAnchor = (RDNode.Anchor)Enum.Parse(typeof(RDNode.Anchor), parentCfg.GetValue("parentSide"));
                            RDNode.Anchor childAnchor = (RDNode.Anchor)Enum.Parse(typeof(RDNode.Anchor), parentCfg.GetValue("childSide"));

                            //Debug.Log("Overriding auto-assignment for node " + treeNode.gameObject.name + " to " + parentAnchor + "->" + childAnchor);
                            connection = new RDNode.Parent(new RDNode.ParentAnchor(parentNode, parentAnchor), childAnchor);

                            parentConnectionsAlreadyProcessed.Add( connection );
                        }
                        else
                        {
                            //create RDNode.Parent structure - anchors will be corrected once all nodes have been loaded
                            connection = new RDNode.Parent(new RDNode.ParentAnchor(parentNode, RDNode.Anchor.RIGHT), RDNode.Anchor.LEFT);
                        }

                        connectionList.Add(connection);
                    }
                    else
                    {
                        Debug.Log("ATC: Invalid parent node specified for: " + treeNode.gameObject.name + " parent: " + parentName);
                    }
                }

            }

            treeNode.parents = connectionList.ToArray();
        }

        private void clearParentsFromNode(RDNode treeNode)
        {
            foreach (RDNode.Parent oldParent in treeNode.parents)
            {
                oldParent.parent.node.children.Remove(treeNode);                  
            }

            Array.Clear(treeNode.parents, 0, treeNode.parents.Count());
        }
    }
}
