using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AI;
using UnityEngine.AI;

public class EnemyNavigation : MonoBehaviour
{
    public Transform target;
    NavMeshAgent agent;
    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateUpAxis = false;
        agent.updateRotation = false;


    }

    // Update is called once per frame
    void Update()
    {
        agent.SetDestination(target.position);
    }
}
