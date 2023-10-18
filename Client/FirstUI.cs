using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstUI : MonoBehaviour
{
    [SerializeField] private GameObject secondObject;

    private void Awake()
    {
    }

    public void PressStart()
    {
        this.gameObject.SetActive(false);
        secondObject.SetActive(true);
    }

}
