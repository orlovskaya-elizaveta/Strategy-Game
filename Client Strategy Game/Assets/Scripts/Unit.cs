using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour {

    public int id;
    public bool isSelected;
    GameObject flag;

    // Use this for initialization
    void Start () {

        isSelected = false;

        flag = Instantiate(Resources.Load<GameObject>("Sphere"));
        flag.transform.SetParent(this.transform);
        flag.transform.localPosition = new Vector3(0, 1f, 0);
        flag.SetActive(false);

    }

    public void ChangeFlag ()
    {
        isSelected = !isSelected;
        flag.SetActive(isSelected);
    }
}
