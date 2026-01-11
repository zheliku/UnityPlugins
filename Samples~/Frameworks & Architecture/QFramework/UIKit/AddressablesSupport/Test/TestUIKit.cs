using QFramework;
using UnityEngine;

public class TestUIKit : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _ = UIKit.OpenPanel<TestPanel>();
    }

    // Update is called once per frame
    void Update()
    {

    }


}