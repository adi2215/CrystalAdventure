using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sine : MonoBehaviour
{
    [SerializeField] private AnimationCurve curve;

    private float _current, _target;

    Vector3 postion;

    bool MoveBlock = true;

    float T = 0;
    float y;
    // Start is called before the first frame update
    void Start()
    {
        y = transform.position.y;
    }

    public void FunctionPos(bool Move, Vector3 currentposition, int i)
    {
        postion = currentposition;
        MoveBlock = Move;
        _target = _target == 0 ? 1 : 0;
        T = i + 1;
    }

    // Update is called once per frame
    void Update()
    {
        if (MoveBlock) 
        {
            GetComponent<Sine>().enabled = false;
        }
        _current = Mathf.MoveTowards(_current, _target, (0.07f + T * 0.01f) * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, postion, curve.Evaluate(_current));
        if (transform.position == postion)
        {
            MoveBlock = true;
        }
    }
}
