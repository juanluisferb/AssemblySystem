using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;


public class ToolDriver : MonoBehaviour
{

    [Header("Prefabs")]
    [Tooltip("The father of the entire screwdriver hierarchy")]
    public GameObject toolParent;
    [Tooltip("The empty prefab of this tool with transform at the tip")]
    public GameObject toolToInstance;
    [Space]
    [Tooltip("TRUE = I WANT the slider for the screw rotation // FALSE = I DON'T WANT the slider for the screw rotation")]
    public bool withSlider = false;
    [Tooltip("The prefab of the Slider")]
    public GameObject slider;

    [Space]

    [Header("Parameters")]
    public float interactMaxTime = 2f;
    public float timeWithoutTrigger = 1f;
    public float maxTime = 0.01f;
    public float screwRotMultiplier = 12f;
    public float stopOffset = 0.03f;
    public float screwSpeed = 0.0001f;


    [Space]

    [Header("Slider")]

    public float sliderOffset;
    [Tooltip("The color the slider starts with (In case #WithSlider = true)")]
    public Color firstColor;
    [Tooltip("The color the slider ends with (In case #WithSlider = true)")]
    public Color secondColor;

    //Privates
    #region Privates

    private float counter = 0;
    private float interactCounter = 0f;

    string toolTag;

    bool isInstanced = false;
    bool isIn;
    bool isOut;
    bool isInAngle = false;
    bool isTool;

    private List<GameObject> currentList;

    private List<GameObject> currentListSorted;

    GameObject currentToolToInstance;

    float maxAngle = 30f;
    float multiplier;

    GameObject screwPto;

    Vector3 prevFrame;

    GameObject[] realHands;

    GameObject currentSlider;

    ScrewLists screwLists;
    #endregion

    private void Start()
    {
        SetList();
        //Initialization of screw variables, set by default
        isIn = true;
        isOut = false;
        isTool = false;
    }


    private void Update()
    {
        //Timers so that an action is not repeated twice in consecutive frames
        TimeCounter();
        InteractCounter();
    }


    private void OnTriggerExit(Collider other)
    {
        //When the trigger is exited, the hands and the real tool are restored and the instantiated objects are hidden.
        if ((other.gameObject.CompareTag("Screw") &&
            this.gameObject.CompareTag("Tool")))
        {
            if (currentToolToInstance != null)
            {
                isInstanced = false;
                // ShowRealHand();
                ShowRealTool();
                ModifyTrigger(false);
                Destroy(currentToolToInstance);
                Destroy(currentSlider);
            }
        }
    }


    private void OnTriggerStay(Collider other)
    {
        //To save lines these comparisons are summarized in the isTool bool
        if ((other.gameObject.CompareTag("Screw") &&
            this.gameObject.CompareTag("Tool")))
        {
            isTool = true;
        }
        else
        {
            isTool = false;
        }


        if (isTool)
        {
            //Take the nearest screw          

            currentListSorted = currentList.OrderBy(x => Vector3.Distance(this.transform.position, x.transform.position)).ToList();

            bool closest = false;

            if (other.gameObject == currentListSorted[0].gameObject)
            {
                closest = true;
            }


            //Tool is angled and not instantized
            if (GetAngle(other.gameObject) && !isInstanced)
            {
                if (SteamVR_Actions._default.GrabPinch.GetState(SteamVR_Input_Sources.Any))
                {

                    if (currentToolToInstance == null)
                    {
                        //Install auxiliary tool
                        currentToolToInstance = Instantiate(toolToInstance, other.transform.GetChild(1).position, other.transform.GetChild(1).rotation);
                        currentToolToInstance.transform.parent = other.transform;

                        if (withSlider)
                        {
                            ShowSlider(other.transform.GetChild(1).transform, other.transform.parent.transform, other.GetComponent<ScrewRotating>().GetDistanceTravelled(), other.GetComponent<Disassembly>().GetScrewLenght());
                        }

                        HideRealTool();
                        isInstanced = true;
                        ModifyTrigger(true);

                        //Link Audio here if you want
                    }

                }

            }

            if (closest)
            {


                //If the button on the remote control is not pressed, the auxiliary tool disappears and the real hand appears with the tool held.
                if (!SteamVR_Actions._default.GrabPinch.GetState(SteamVR_Input_Sources.Any))
                {
                    if (currentToolToInstance != null)
                    {
                        isInstanced = false;
                        ShowRealTool();
                        ModifyTrigger(false);
                        Destroy(currentToolToInstance);
                        Destroy(currentSlider);
                    }
                }

                //If the tool is not at an angle and the auxiliary is instantiated, the auxiliary disappears and the real hand appears with the tool held.
                if (!GetAngle(other.gameObject) && isInstanced)
                {
                    if (currentToolToInstance != null)
                    {
                        ShowRealTool();
                        ModifyTrigger(false);
                        isInstanced = false;
                        Destroy(currentToolToInstance);
                        Destroy(currentSlider);


                    }
                }

                //If the tool is at an angle and the control button is held down and the auxiliary tool is instantiated, proceed to rotate the tool.
                if (GetAngle(other.gameObject) &&
                   (SteamVR_Actions._default.GrabPinch.GetState(SteamVR_Input_Sources.Any)) &&
                    isInstanced)
                {
                    ModifyTrigger(true);
                    RotateTool(other.gameObject);
                }
            }
        }
    }

    //This function is the one that exerts the rotational movement of all the screws in relation to the rotation of the hidden tool held in the hand.
    //Each frame is called in OnTriggerStay and is the main function of this script.
    void RotateTool(GameObject screw)
    {
        if (withSlider)
        {
            //Updates the slider (in the fillAmount) with the distance traveled by the screw in its space.
            currentSlider.transform.GetChild(0).GetComponent<Image>().fillAmount = screw.GetComponent<ScrewRotating>().GetDistanceTravelled() * multiplier;
            //Updates the position of the slider to follow the screw head
            currentSlider.transform.position = screw.transform.GetChild(1).transform.position;

            //Makes a Lerp between two public Colors that modify the color of the slider by means of its fillAmount
            currentSlider.transform.GetChild(0).GetComponent<Image>().color = Color.Lerp(firstColor, secondColor, currentSlider.transform.GetChild(0).GetComponent<Image>().fillAmount);
        }

        //Calculates an angle (float) between the Up vector of the HoveringPose Transform between the previous frame and the current frame.
        float _angleVectors = Vector3.SignedAngle(prevFrame, this.transform.up, this.transform.forward);


        //Checks whether the float obtained above is positive or negative to check whether it is being screwed or unscrewed.
        if ((_angleVectors > 0 && _angleVectors <= stopOffset) || (_angleVectors < 0 && _angleVectors >= -stopOffset) &&
            counter >= maxTime)
        {
            counter = 0f;
        }


        //SCREW IN
        //It performs the action of placing the screw right-side up if it is not already in place.
        if (screw.GetComponent<ScrewRotating>().GetDistanceTravelled() > 0)
        {
            if (_angleVectors < 0 + stopOffset && counter >= maxTime)
            {
                screw.transform.Rotate(Vector3.forward * screwRotMultiplier, Space.Self);
                screw.transform.Translate(-Vector3.forward * screwSpeed, Space.Self);

                screw.GetComponent<ScrewRotating>().SetDistanceTravelled(-screwSpeed);


                counter = 0f;
            }
        }


        //SCREW OUT
        //It performs the action of placing the screw right-side up if it is not outside its place
        if (screw.GetComponent<ScrewRotating>().GetDistanceTravelled() <= (screw.GetComponent<Disassembly>().GetScrewLenght()))
        {

            if (_angleVectors > 0 - stopOffset && counter >= maxTime)
            {
                screw.transform.Rotate(-Vector3.forward * screwRotMultiplier, Space.Self);
                screw.transform.Translate(Vector3.forward * screwSpeed, Space.Self);

                screw.GetComponent<ScrewRotating>().SetDistanceTravelled(screwSpeed);

                counter = 0f;
            }
        }




        //SCREW IN CHECK
        //Check the position of the screw to see if it is in place (at the moment when it starts to go in).
        if (screw.GetComponent<ScrewRotating>().GetDistanceTravelled() < (screw.GetComponent<Disassembly>().GetScrewLenght()) &&
            !isIn &&
            isOut)
        {
            screw.GetComponent<Disassembly>().AssemblyScrew();
            isIn = true;
            isOut = false;

        }
        //If the screw is all the way in, the interacting action ends
        if (screw.GetComponent<ScrewRotating>().GetDistanceTravelled() <= 0f && interactCounter >= interactMaxTime)
        {
            isInstanced = false;
            // ShowRealHand();
            ShowRealTool();
            ModifyTrigger(false);
            DeactivateTrigger();
            Invoke("ActivateTrigger", timeWithoutTrigger);

            Destroy(currentToolToInstance);
            Destroy(currentSlider);

            interactCounter = 0;
        }

        //SCREW OUT CHECK
        //Check the position of the screw to see if it is out of place.
        if (screw.GetComponent<ScrewRotating>().GetDistanceTravelled() >= (screw.GetComponent<Disassembly>().GetScrewLenght()) &&
        !isOut &&
        isIn)
        {
            screw.GetComponent<Disassembly>().DisassemblyScrew();
            isOut = true;
            isIn = false;

            //If the screw is completely out, the interacting action is terminated.
            if (interactCounter >= interactMaxTime)
            {
                isInstanced = false;
                ShowRealTool();
                ModifyTrigger(false);
                DeactivateTrigger();
                Invoke("ActivateTrigger", timeWithoutTrigger);

                Destroy(currentToolToInstance);
                Destroy(currentSlider);

                interactCounter = 0;

                //Audio
            }

        }
        //Sets the position of prevFrame (of the Up vector) to compare in the next frame
        prevFrame = this.transform.up;

    }



    //Check that the tool is at an angle to the screw and if is the closest element to interact with the screw 
    bool GetAngle(GameObject other)
    {

        float currentAngle = Vector3.Angle(this.transform.forward, -other.transform.forward);
        
        if (currentAngle <= maxAngle && other == currentListSorted[0])
        {
            isInAngle = true;
        }
        else
        {
            isInAngle = false;
        }

        return isInAngle;
    }

    //Contador de tiempo
    void TimeCounter()
    {
        counter += Time.deltaTime;
    }

    //Contador de tiempo
    void InteractCounter()
    {
        interactCounter += Time.deltaTime;
    }



    //Hide the tool (Mesh Renderer) that the user is holding by hand (scrolling lists in case a tool has more than one visible mesh in its children).
    void HideRealTool()
    {
        if (toolParent.gameObject.GetComponent<Disassembly>() != null)
        {
            this.transform.parent.transform.parent.GetComponent<MeshRenderer>().enabled = false;
            this.transform.parent.transform.parent.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
            this.transform.parent.transform.parent.GetChild(1).GetComponent<MeshRenderer>().enabled = false;
        }

        if (toolParent.GetComponent<MeshRenderer>() != null)
        {
            toolParent.GetComponent<MeshRenderer>().enabled = false;
            toolParent.GetComponent<MeshCollider>().enabled = false;
        }

        if (toolParent.transform.GetComponentInChildren<MeshRenderer>() != null)
        {

            foreach (Transform child in toolParent.transform)
            {
                if (child.GetComponent<MeshRenderer>() != null)
                {
                    child.GetComponent<MeshRenderer>().enabled = false;
                    child.GetComponent<MeshCollider>().enabled = false;

                    if (child.transform.GetComponentInChildren<MeshRenderer>())
                    {
                        foreach (Transform child2 in child)
                        {
                            child2.GetComponent<MeshRenderer>().enabled = false;
                            child2.GetComponent<MeshCollider>().enabled = false;
                        }
                    }
                }
            }
        }
    }


    //Re-display the previously hidden tool (by scrolling through lists as above)
    void ShowRealTool()
    {
        if (toolParent.gameObject.GetComponent<Disassembly>() != null)
        {
            this.transform.parent.transform.parent.GetComponent<MeshRenderer>().enabled = true;
            this.transform.parent.transform.parent.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
            this.transform.parent.transform.parent.GetChild(1).GetComponent<MeshRenderer>().enabled = true;
        }

        if (toolParent.GetComponent<MeshRenderer>() != null)
        {
            toolParent.GetComponent<MeshRenderer>().enabled = true;

            if (toolParent.GetComponent<Disassembly>() == null)
            {
                toolParent.GetComponent<MeshCollider>().enabled = true;
            }


            if (toolParent.GetComponent<Disassembly>() != null)
            {
                toolParent.GetComponent<MeshCollider>().enabled = false;
            }

        }

        if (toolParent.transform.GetComponentInChildren<MeshRenderer>() != null)
        {

            foreach (Transform child in toolParent.transform)
            {
                if (child.GetComponent<MeshRenderer>() != null)
                {
                    child.GetComponent<MeshRenderer>().enabled = true;
                    child.GetComponent<MeshCollider>().enabled = true;

                    if (child.transform.GetComponentInChildren<MeshRenderer>())
                    {
                        foreach (Transform child2 in child)
                        {
                            child2.GetComponent<MeshRenderer>().enabled = true;
                            child2.GetComponent<MeshCollider>().enabled = true;
                        }
                    }
                }
            }
        }
    }



    //Modify the size of the trigger for smoother rotation during interaction
    void ModifyTrigger(bool state)
    {
        if (state)
        {
            this.gameObject.GetComponent<SphereCollider>().radius = 0.02f;
        }
        if (!state)
        {
            this.gameObject.GetComponent<SphereCollider>().radius = 0.01f;
        }
    }

    //Deactivate the trigger for a period of time to avoid that when you stop interacting by removing or inserting the screw, you interact again by mistake.
    void DeactivateTrigger()
    {

        this.GetComponent<SphereCollider>().enabled = false;

    }

    //Reactivates the trigger deactivated in the previous function
    void ActivateTrigger()
    {

        this.GetComponent<SphereCollider>().enabled = true;

    }

    //Install the Radial Slider around the screw and set its rotation and its value in fillAmount.
    void ShowSlider(Transform place, Transform parent, float amount, float lenght)
    {
        currentSlider = Instantiate(slider, place.position + new Vector3(0, 0, -sliderOffset), place.rotation, parent);

        currentSlider.transform.rotation = place.parent.gameObject.GetComponent<ScrewRotating>().GetScrollRotation();

        //This works as a rule of 3 since a different value is needed for each screw length.
        multiplier = 1 / lenght;
        currentSlider.transform.GetChild(0).GetComponent<Image>().fillAmount = amount * multiplier;
    }


    public void SetList()
    {
        screwLists = GameObject.FindGameObjectWithTag("ScrewList").GetComponent<ScrewLists>();

        if (this.gameObject.CompareTag("Tool"))
        {
            currentList = screwLists.screw;
        }

    }


    public void ClearLists()
    {
        currentList.Clear();
        Invoke("SetList", .75f);
    }

}

