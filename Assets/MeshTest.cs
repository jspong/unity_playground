using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer)),
    RequireComponent(typeof(Rigidbody)),
    RequireComponent(typeof(MeshFilter)),
    RequireComponent(typeof(Collider))]
public class MeshTest : MonoBehaviour
{

    class Spring
    {
        public readonly int a;
        public readonly int b;

        private readonly Vector3[] vertices;
        private readonly Vector3[] velocities;
        private readonly float restLength;

        public float stiffness = 0.5f;
        public float damping = 0.20f;

        public Spring(int a, int b, Vector3[] vertices, Vector3[] velocities)
        {
            this.a = (int)Mathf.Min(a, b);
            this.b = (int)Mathf.Max(a, b);

            this.vertices = vertices;
            this.velocities = velocities;

            restLength = (vertices[b] - vertices[a]).magnitude;
        }

        public Vector3 Force()
        {
            Vector3 distance = vertices[b] - vertices[a];

            Vector3 direction = distance.normalized;
            float compression = distance.magnitude - restLength;
            Vector3 velocity = velocities[b] - velocities[a];
            float force = compression * stiffness + Vector3.Dot(velocity, direction) * damping;

            return direction * force;
        }

        public override bool Equals(object obj)
        {
            Spring spring = obj as Spring;
            if (spring == null)
            {
                return false;
            }
            return a == spring.a && b == spring.b;
        }

        public override int GetHashCode()
        {
            return (a, b).GetHashCode();
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        List<ContactPoint> contacts = new List<ContactPoint>();
        int count = collision.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            var cp = contacts[i];
            int closest = FindClosest(cp.point, vertices);
            int master = constraints[closest];
            foreach (int v in reverseConstraints[master])
            {
                vertices[v] -= mesh.normals[v] * 0.2f;
            }
        }
    }

    public int FindClosest(Vector3 point, Vector3[] vertices)
    {
        int index = -1;
        float minDistance = float.MaxValue;

        int i = 0;
        foreach (Vector3 vertex in vertices)
        {
            float distance = Vector3.Distance(vertex, point);
            if (distance < minDistance)
            {
                index = i;
                minDistance = distance;
            }
            i++;
        }
        return index;
    }

    Mesh mesh;
    MeshRenderer mr;
    Rigidbody rb;

    Vector3[] accelerations;
    Vector3[] vertices;
    Vector3[] velocities;
    HashSet<Spring> springs;
    Dictionary<int, int> constraints;
    Dictionary<int, int> scalars;
    Dictionary<int, HashSet<int>> reverseConstraints;

    // Start is called before the first frame update
    void Start()
    {
        constraints = new Dictionary<int, int>();
        reverseConstraints = new Dictionary<int, HashSet<int>>();
        scalars = new Dictionary<int, int>();
        rb = GetComponent<Rigidbody>();
        springs = new HashSet<Spring>();
        mesh = GetComponent<MeshFilter>().mesh;
        vertices = mesh.vertices;
        velocities = new Vector3[vertices.Length];
        accelerations = new Vector3[vertices.Length];

        int[] t = mesh.triangles;

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!constraints.ContainsKey(i))
            {
                constraints[i] = i;
                scalars[i] = 1;
                reverseConstraints[i] = new HashSet<int>();
                reverseConstraints[i].Add(i);
            }
            for (int j = i+1; j < vertices.Length; j++)
            {
                if (constraints.ContainsKey(j))
                {
                    continue;
                }
                if (vertices[i].Equals(vertices[j]))
                {
                    constraints[j] = i;
                    reverseConstraints[i].Add(j);
                    scalars[i] += 1;
                }
            }
        }

        for (int face = 0; face < t.Length / 3; face++)
        {
            int a = constraints[t[face * 3 + 0]];
            int b = constraints[t[face * 3 + 1]];
            int c = constraints[t[face * 3 + 2]];
            springs.Add(new Spring(a, b, vertices, velocities));
            springs.Add(new Spring(b, c, vertices, velocities));
            springs.Add(new Spring(c, a, vertices, velocities));
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector3[] forces = new Vector3[vertices.Length];

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
           
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 localPoint = hit.transform.InverseTransformPoint(hit.point);
                int i = FindClosest(localPoint, vertices);
                vertices[i] -= mesh.normals[i] * 0.2f;
            }
        }

        foreach (Spring spring in springs)
        {
            Vector3 force = spring.Force();
            forces[spring.a] += force;
            forces[spring.b] -= force;
        }

        foreach (KeyValuePair<int, int> kvp in constraints)
        {
            accelerations[kvp.Key] = forces[kvp.Value] / scalars[kvp.Value];
            vertices[kvp.Key] += velocities[kvp.Value] * Time.deltaTime + accelerations[kvp.Value] / 2.0f * Time.deltaTime;
            velocities[kvp.Key] += accelerations[kvp.Value] * Time.deltaTime;
        }

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

    }
}
