using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class Disassembly : MonoBehaviour
{
    bool interact;

    bool removable;

    [Tooltip("List of parts THAT limit the movement of the selected part")]
    [HideInInspector]
    public List<GameObject> holders;

    [Tooltip("List of parts TO WHICH the selected part restricts the movement")]
    public List<GameObject> clamping;

    Vector3 place;
    string parentName;
    bool isGrabbed;
    bool oneTime = true;
    bool oneTimeTwo;
    bool canAssembly;
    bool isRightHand;
    bool isLeftHand;

    Interactable interactable;

    private GameObject referenceObject;

    [Space]

    [Tooltip("Maximum distance between this object and its referenceObject with which, when the part is released, it is assembled")]
    public float distanceToAssembly = 0.1f;

    Vector3 offset;

    float lenght;

    private bool isPlaced = false;

    private bool isScrewed = true;

    float dist;

    private bool isTool;
    bool isAssembled = true;

    ScrewLists screwLists;
    [HideInInspector]
    public List<GameObject> currentListOfScrew;
    [HideInInspector]
    public List<GameObject> currentListOfScrewSorted;

    private Transform OGParent;

    //Creates auxiliary lists so that one screw becomes another when it adopts its position
    [Header("Aux")]
    List<GameObject> auxHolders;
    List<GameObject> auxclampers;
    Transform initPos;



    public float GetScrewLenght()
    {
        return lenght;
    }


    [Space]
    [Header("Assembly Animation")]
    float speedAnim = 12f;
    bool isAssemblying = false;


    private void Awake()
    {
        interactable = transform.GetComponent<Interactable>();

        OGParent = transform.parent;


        CheckTag();
    }

    private void Start()
    {
        foreach (GameObject clamper in clamping)
        {
            clamper.GetComponent<Disassembly>().holders.Add(this.gameObject);
        }
        //At the start of the application we need to create the lists of holders, clampings and their reference objects.
        //Basically in Start() the structure of the composed object is organized in the variables dragged to this script in the editor
        CheckHolders();
        PieceController();

        screwLists = GameObject.FindGameObjectWithTag("ScrewList").GetComponent<ScrewLists>();
        if (isTool)
        {
            CreateMimicObject();
        }


        auxHolders = holders;
        auxclampers = clamping;
        initPos = transform;


    }

    public void OnInstantiate()
    {
        //If you want to start again (Not Used at the moment)
        CheckHolders();
        PieceController();
        if (isTool)
        {
            CreateMimicObject();
        }
    }

    private void Update()
    {
        isRightHand = SteamVR_Actions.default_GrabGrip.GetStateUp(SteamVR_Input_Sources.RightHand);
        isLeftHand = SteamVR_Actions.default_GrabGrip.GetStateUp(SteamVR_Input_Sources.LeftHand);

        if (isAssemblying)
        {
            AssemblyAnimation();
        }

        if (isTool)
        {
            CheckGrab();
        }

        ScrewController();

        //It is necessary to check in each frame whether the object is caught or not in order to give it the possibility to be mounted or not.
        if (isGrabbed == true)
        {
            CheckAssembly();
            AssemblyObject();
        }

        if (!isTool)
        {
            CheckGrab();
        }


        //The OneTime is used as a control variable to check that this is called only once.
        //In case the part is caught we need to modify the lists of the objects that are related to it
        if (isGrabbed == true && oneTime == true)
        {
            oneTime = false;

            RemoveClampingObjects();
            OtherCheckHolders();

            OtherPieceController();

        }

        GravityController();

    }

    public void CheckHolders()
    {
        //Check if the part in question has holders. If it does not, it makes it removable and can be picked up.
        if (holders.Count == 0)
        {
            //IsTool checks if it is the screw
            if (!isTool)
            {
                removable = true;
            }
            else
            {
                isScrewed = true;
            }
        }
        else
        {
            removable = false;
        }
    }

    void GravityController()
    {
        //Provides gravity to objects that are detached from the bicycle
        if (referenceObject != null)
        {
            if (isAssembled || isAssemblying)
            {
                this.gameObject.GetComponent<Rigidbody>().isKinematic = true;
            }

            if (!isGrabbed && !isAssembled)
            {
                if (!isAssemblying)
                {
                    this.gameObject.GetComponent<Rigidbody>().isKinematic = false;
                }

            }
        }
    }

    void CheckGrab()
    {
        //Checks if the object is picked up with either hand

        if (this.transform.parent != null)
        {
            if ((interactable != null && interactable.attachedToHand != null))
            {
                isGrabbed = true;
                isAssembled = false;

                if (isTool)
                {
                    isPlaced = false;

                    //These if statements are used to add the referenced objects to the lists of objects in which a screw can be placed, checking if they previously exist in the lists.

                    if (this.transform.CompareTag("Screw") && !screwLists.screwReferences.Contains(referenceObject))
                    {
                        screwLists.screwReferences.Add(referenceObject);
                    }

                    GetCurrentListOfScrew();
                }

            }
            else
            {
                isGrabbed = false;
                oneTime = true;
            }
        }
        else
        {
            isGrabbed = false;
        }
    }

    void OtherPieceController()
    {
        //Calls the PieceController of the parts that are in the clamping list of this object.
        foreach (GameObject item in clamping)
        {
            item.GetComponent<Disassembly>().PieceController();
        }
    }

    void OtherCheckHolders()
    {
        //Call the CheckHolders of the parts that are in the list of campsites of this object.
        foreach (GameObject item in clamping)
        {
            item.GetComponent<Disassembly>().CheckHolders();
        }
    }

    public void PieceController()
    {
        //If the part is removable it provides the necessary scripts to be able to be interacted with, it also creates a reference object by calling CreateMimicObject
        //It also does the opposite step in case it is not removable.
        if (removable == true)
        {
            interactable.highlightOnHover = true;
            interactable.enabled = true;

            if (!this.gameObject.GetComponent<Throwable>())
            {
                this.gameObject.AddComponent<Throwable>();
            }


            if (isTool)
            {
                if (referenceObject == null)
                {
                    CreateMimicObject();
                }
            }
            else
            {
                CreateMimicObject();
            }
        }

        if (!removable)
        {
            interactable.highlightOnHover = false;
            interactable.enabled = false;

            if (this.gameObject.GetComponent<Throwable>() != null)
            {
                Destroy(this.gameObject.GetComponent<Throwable>());

            }
        }
    }

    public void RemoveClampingObjects()
    {
        foreach (var item in clamping)
        {
            List<GameObject> tempHolder = new List<GameObject>();
            tempHolder = item.GetComponent<Disassembly>().holders;


            foreach (var holder in tempHolder.ToList())
            {
                if (holder.name == this.gameObject.name)
                {
                    item.GetComponent<Disassembly>().holders.Remove(holder);
                    OtherCheckHolders();
                    OtherPieceController();
                    RemoveClampingObjects();
                }
            }
        }
    }

    public void AddHolderObjects()
    {
        //Adds objects to the list of holders when they are put back in place
        oneTimeTwo = false;

        foreach (GameObject item in clamping)
        {
            if (item != null)
            {
                item.GetComponent<Disassembly>().holders.Add(this.gameObject);
            }
        }
    }

    void CreateMimicObject()
    {
        //Create a reference object to know where the object should be placed once it is removed.
        if (!isTool)
        {
            referenceObject = Instantiate(new GameObject(), this.transform.position, this.transform.rotation, this.transform.parent);
            referenceObject.name = this.gameObject.name + "(REFERENCE)";
        }


        if (isTool)
        {
            //In the case of screws, this script creates the reference object and calculates its offset by taking the distance between the screw head and the base.
            if (this.gameObject.transform.childCount > 0)
            {
                GameObject top = this.transform.GetChild(1).gameObject;
                GameObject bottom = this.transform.GetChild(0).gameObject;
                lenght = Vector3.Distance(top.transform.position, bottom.transform.position);
                // lenght *= 2;
                offset = new Vector3(0f, 0f, lenght * 6);


                referenceObject = Instantiate(new GameObject(), Vector3.zero, Quaternion.identity);
                referenceObject.transform.parent = this.transform;
                referenceObject.name = this.gameObject.tag + "(REFERENCE)";

                referenceObject.transform.localPosition = Vector3.zero + offset;
                referenceObject.transform.localRotation = this.transform.localRotation;
                Quaternion newRot = Quaternion.Euler(0f, 0f, 0f);
                referenceObject.transform.localRotation = newRot;
                referenceObject.transform.parent = this.transform.parent;

                initPos = referenceObject.transform;

            }
        }
    }

    void CheckAssembly()
    {
        //Checks the distance at which the part can be placed when it is released

        if (!isTool)
        {
            dist = Vector3.Distance(this.transform.position, referenceObject.transform.position);

            if (dist <= distanceToAssembly && isGrabbed)
            {
                canAssembly = true;
            }
            else
            {
                canAssembly = false;
            }
        }

        if (isTool)
        {
            //If "is screw" there is no static reference object but it will always be the first in a list that sorts them by distance.
            currentListOfScrewSorted = currentListOfScrew.OrderBy(x => Vector3.Distance(this.transform.position, x.transform.position)).ToList();
            dist = Vector3.Distance(this.transform.position, currentListOfScrewSorted[0].transform.position);

            referenceObject = currentListOfScrewSorted[0];

            if (dist <= distanceToAssembly && isGrabbed)
            {
                canAssembly = true;
            }
            else
            {
                canAssembly = false;
            }
        }

    }

    void AssemblyObject()
    {
        //This method controls when a part is placed and calls the methods that reorder the hierarchy and lists of the script
        if (canAssembly == true)
        {
            GetCurrentListOfScrew();
            oneTimeTwo = true;

            if (isTool)
            {

                initPos = referenceObject.transform;
            }



            if ((isLeftHand || isRightHand)
               && oneTimeTwo == true)
            {

                this.transform.SetParent(OGParent);

                isAssemblying = true;


                if (isTool)
                {
                    isPlaced = true;
                    this.GetComponent<ScrewRotating>().ResetDistanceTravelled(lenght);

                    //The following if statements check if the reference object (when a screw is placed on it) still exists as "usable" in your list
                    //and if so it is placed and removed from its own list and from other screws that can also be placed in that position.

                    if (this.transform.CompareTag("Screw") && screwLists.screwReferences.Contains(referenceObject))
                    {
                        screwLists.screwReferences.Remove(referenceObject);

                        foreach (GameObject screw in screwLists.screw)
                        {
                            if (screw.GetComponent<Disassembly>().currentListOfScrew.Contains(referenceObject))
                            {
                                screw.GetComponent<Disassembly>().currentListOfScrew.Remove(referenceObject);

                            }

                            if (screw.GetComponent<Disassembly>().currentListOfScrewSorted.Contains(referenceObject))
                            {
                                screw.GetComponent<Disassembly>().currentListOfScrewSorted.Remove(referenceObject);
                            }

                        }
                    }

                    GetCurrentListOfScrew();
                }


                if (!isTool)
                {
                    removable = false;

                    AddHolderObjects();
                    OtherCheckHolders();
                    OtherPieceController();
                    transform.SetParent(OGParent);
                }
            }
        }
    }



    void AssemblyAnimation()
    {
        //Linear interpolation between positions to create a montage animation
        if (this.transform.position == referenceObject.transform.position)
        {
            isAssemblying = false;
            isAssembled = true;

            if (isTool)
            {
                //The following if statements control the substitution of one part for another using the auxiliary lists.
                //If we take a screw and place it in the place where there was another one, it will adopt the lists (of clampers and holders) and the reference position of the replaced screw 

                if (this.transform.CompareTag("Screw"))
                {
                    foreach (GameObject screw in screwLists.screw)
                    {
                        if (transform.position == screw.GetComponent<Disassembly>().initPos.position)
                        {
                            holders = screw.GetComponent<Disassembly>().auxHolders;
                            clamping = screw.GetComponent<Disassembly>().auxclampers;
                            initPos = this.transform;

                            screw.GetComponent<Disassembly>().holders = auxHolders;
                            screw.GetComponent<Disassembly>().clamping = auxclampers;

                            screw.GetComponent<Disassembly>().auxHolders = auxHolders;
                            screw.GetComponent<Disassembly>().auxclampers = auxclampers;

                            auxHolders = holders;
                            auxclampers = clamping;
                        }
                    }
                }
            }
        }

        transform.position = Vector3.Lerp(transform.position, referenceObject.transform.position, Time.deltaTime * speedAnim);
        transform.rotation = Quaternion.Lerp(transform.rotation, referenceObject.transform.rotation, Time.deltaTime * speedAnim);
        transform.parent = OGParent;
    }

    public void AssemblyScrew()
    {
        //This is the process of turning the screw

        removable = false;

        PieceController();

        AddHolderObjects();
        OtherCheckHolders();
        OtherPieceController();

        isPlaced = false;
        isScrewed = true;
    }

    public void DisassemblyScrew()
    {
        //Function for unscrewing the screw

        removable = true;
        PieceController();

        isPlaced = true;
        isScrewed = false;
    }

    void ScrewController()
    {
        //Check whether the screw is tightened or not to make it removable.
        if (isScrewed == false && isTool)
        {
            removable = true;
        }

    }

    void CheckTag()
    {
        //This method simplifies tag comparison
        if (this.gameObject.CompareTag("Screw"))
        {
            isTool = true;
        }
        else
        {
            // is not necessary, it is only for control. In the Awake() it should already be set to False if the If() is not fulfilled.
            isTool = false;
        }
    }

    //This function updates the "current" lists from the ScrewLists class to keep all the reference objects in the scene up to date.

    public void GetCurrentListOfScrew()
    {
        if (this.transform.CompareTag("Screw") && !isPlaced && !isScrewed)
        {
            currentListOfScrew = screwLists.screwReferences;
        }
    }

    public void AddScrewScript()
    {
        if (this.GetComponent<ScrewRotating>() == null && (this.gameObject.CompareTag("ScrewStar")))
        {
            this.gameObject.AddComponent<ScrewRotating>();
        }
    }


}
