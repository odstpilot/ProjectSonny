using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngel : MonoBehaviour
{
    public Transform player;
    public Transform resetPoint;
    public float detectionRadius = 10f;
    public LayerMask lightLayer;
    public float stunDuration = 2f;

    private NavMeshAgent agent;
    private bool isStunned = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning("Agent is not on a NavMesh at start!");
        }
    }

    private void Update()
    {
        if (isStunned) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, lightLayer);
        if (hit == null)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            collision.transform.position = resetPoint.position;
        }
    }

    public void Stun()
    {
        if (!isStunned)
        {
            StartCoroutine(StunCoroutine());
        }
    }

    private IEnumerator StunCoroutine()
    {
        isStunned = true;
        agent.isStopped = true;
        agent.ResetPath();

        yield return new WaitForSeconds(stunDuration);

        isStunned = false;
        agent.isStopped = false;
    }
}
