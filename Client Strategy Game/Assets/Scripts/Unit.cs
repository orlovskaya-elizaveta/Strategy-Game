using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour {

    public int id;
    public bool isSelected;
    GameObject flag;
    private Animator animator;
    private CharState State
    {
        get { return (CharState)animator.GetInteger("State"); }
        set { animator.SetInteger("State", (int)value); }
    }

    void Start () {

        isSelected = false;
        animator = GetComponent<Animator>();
        flag = Instantiate(Resources.Load<GameObject>("Sphere"));
        flag.transform.SetParent(this.transform);
        flag.transform.localPosition = new Vector3(0, 3f, 0);
        flag.SetActive(false);
        State = CharState.Idle;
    }

    public void SetFlag (bool isSelected)
    {
        this.isSelected = isSelected;
        flag.SetActive(isSelected);
    }

    public void SetRunAnimation (bool isWalking)
    {
        if (isWalking)
            State = CharState.Run;
        else
            State = CharState.Idle;
    }

    public enum CharState
    {
        Idle,
        Run
    }

}
