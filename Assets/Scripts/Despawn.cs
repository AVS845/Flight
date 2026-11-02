using UnityEngine;

public class Despawn : MonoBehaviour
{
    public float lifetime;
    public float despawnSmoothing;

    private float age;
    private bool aging = false;
    private bool despawning = false;
    
    void Update()
    {
        if (aging)
        {
            if (!despawning)
            {
                age += Time.deltaTime;

                if (age >= lifetime) despawning = true;
            }
            else if (transform.localScale.magnitude > 0.1)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, despawnSmoothing * Time.deltaTime);
            }
            else StopLife();
        }
    }

    public void StartLife()
    {
        age = 0;
        aging = true;
        despawning = false;
        transform.localScale = Vector3.one * 0.3f;
    }

    public void StartDespawn()
    {
        despawning = true;
    }

    void StopLife()
    {
        aging = false;
        gameObject.SetActive(false);
    }
}
