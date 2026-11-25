using System.Collections.Generic;
using UnityEngine;

namespace CodingGame.Defense
{
    // The look of a tree legion (Enemy.Tree) node on its DefenseActorView. In the simulation every node stands where the root
    // stands; here the root walks on its root legs (waddling) and each node below it sits up on its branches: depth d at
    // HeightOf(d), spread left to right by its slot as the camera sees it (so the tree always reads as a tree on screen). Each view draws the branches to its own children: brown while the child lives,
    // withered (grey, drooping, short) once it died, with a puff of leaves that moment. A node that can be hit (no living
    // children) glows in a pulsing halo and is tinted bright; one with living children is tinted dark like closed bark.
    // ponytail: hits, shots and death sparks still land at the root's feet (the simulation's position), not up on the branch.
    public sealed class DefenseTreeView : MonoBehaviour
    {
        const float Width = 2.6f, FirstHeight = 1.45f, Step = .6f, TrunkTop = 1.15f;
        static Material branchMaterial, leafMaterial;
        EnemyState node;
        Transform[] legs;
        Quaternion[] legRest;
        Renderer halo;
        readonly Transform[] branches = new Transform[2];
        readonly bool[] withered = new bool[2];

        public static DefenseTreeView Of(DefenseActorView view) => view.TryGetComponent<DefenseTreeView>(out var tree) ? tree : view.gameObject.AddComponent<DefenseTreeView>();
        static float HeightOf(int depth) => FirstHeight + (depth - 2) * Step;
        static float ScaleOf(int depth) => depth <= 1 ? 1 : 1.05f - .15f * depth;
        // Where a node stands in the world, from where its root stands: right and away are the camera's, level.
        static Vector3 Spot(TreeNode n, Vector3 root, Vector3 right, Vector3 away)
        {
            if (n.Parent == null) return root;
            float across = ((n.Slot + .5f) / (1 << (n.Depth - 1)) - .5f) * Width;
            return root + right * across + Vector3.up * HeightOf(n.Depth) + away * (.12f * (n.Depth - 1));
        }
        static Vector3 Anchor(TreeNode n, Vector3 at) => n.Parent == null ? at + Vector3.up * TrunkTop : at;

        // Called from DefenseActorView.SyncEnemy: where the node stands (the root walks its own way, the others face the camera).
        public Vector3 Place(EnemyState enemy, double time, Quaternion viewRotation)
        {
            node = enemy;
            var n = enemy.Node;
            var root = DefenseViews.World(n.Root.Position);
            if (n.Parent == null)
            {
                Waddle(enemy, time);
            }
            transform.localScale = Vector3.one * ScaleOf(n.Depth);
            var right = viewRotation * Vector3.right; right.y = 0; right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
            var away = Vector3.Cross(right, Vector3.up);
            var at = Spot(n, root, right, away);
            if (n.Parent != null) transform.rotation = Quaternion.LookRotation(-away); // the leaf faces look at the player
            ShowHalo(n.Open, time);
            for (int side = 0; side < 2; side++) Branch(side, side == 0 ? n.Left : n.Right, Anchor(n, at), root, right, away);
            return at;
        }
        // Bright when it can be hit, dark bark while a child still lives.
        public static Color Tint(EnemyState enemy, double time) => enemy.Node.Open
            ? Color.Lerp(Color.white, new Color(1.35f, 1.35f, 1.05f), .5f + .5f * Mathf.Sin((float)time * 7))
            : new Color(.58f, .52f, .5f);

        void Waddle(EnemyState enemy, double time)
        {
            if (legs == null)
            {
                var found = new List<Transform>();
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Leg")) found.Add(t);
                legs = found.ToArray(); legRest = new Quaternion[legs.Length];
                for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i].localRotation;
            }
            bool walking = enemy.BlockedBy == 0 && !enemy.Stunned(time);
            float phase = walking ? (float)time * 9 : 0;
            for (int i = 0; i < legs.Length; i++) legs[i].localRotation = legRest[i] * Quaternion.Euler(28 * Mathf.Sin(phase + i * Mathf.PI), 0, 0);
        }
        void ShowHalo(bool open, double time)
        {
            if (!open) { if (halo) halo.enabled = false; return; }
            if (!halo)
            {
                halo = GetComponent<DefenseActorView>().Shell("Hittable halo", PrimitiveType.Sphere, ref haloMesh, ref haloMaterial, new Color(1, .95f, .3f, .5f));
                halo.transform.localPosition = node.Node.Parent == null ? Vector3.up * .7f : Vector3.zero;
                halo.transform.localScale = node.Node.Parent == null ? new Vector3(1.1f, 1.7f, 1.1f) : Vector3.one * .95f;
            }
            halo.enabled = true;
            float pulse = 1 + .08f * Mathf.Sin((float)time * 7);
            halo.transform.localScale = (node.Node.Parent == null ? new Vector3(1.1f, 1.7f, 1.1f) : Vector3.one * .95f) * pulse;
        }
        static Mesh haloMesh; static Material haloMaterial;
        // The branch from this node up to one child: a brown stick, or a withered grey one once that child died.
        void Branch(int side, EnemyState child, Vector3 from, Vector3 root, Vector3 right, Vector3 away)
        {
            if (child == null) return;
            var to = Spot(child.Node, root, right, away);
            if (!branches[side])
            {
                if (!branchMaterial)
                {
                    branchMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tree branch" };
                    branchMaterial.SetColor("_BaseColor", new Color(.55f, .38f, .26f));
                }
                var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(stick.GetComponent<Collider>());
                stick.name = "Branch"; stick.layer = gameObject.layer; stick.transform.SetParent(transform, true);
                stick.GetComponent<Renderer>().sharedMaterial = branchMaterial;
                branches[side] = stick.transform;
            }
            if (!child.Active && !withered[side])
            {
                withered[side] = true;
                branches[side].GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(.5f, .47f, .43f));
                Leaves(to);
            }
            if (withered[side]) to = from + (to - from) * .45f + Vector3.down * .3f; // drooping, half as long
            var span = to - from;
            branches[side].SetPositionAndRotation(from + span * .5f, Quaternion.FromToRotation(Vector3.up, span));
            float thick = (withered[side] ? .05f : .09f) * ScaleOf(child.Node.Depth) / Mathf.Max(.01f, transform.lossyScale.x);
            branches[side].localScale = new Vector3(thick, span.magnitude * .5f / Mathf.Max(.01f, transform.lossyScale.x), thick);
        }
        // A puff of green leaves where a node died, falling.
        void Leaves(Vector3 at)
        {
            var go = new GameObject("Leaves", typeof(ParticleSystem));
            go.layer = gameObject.layer; go.transform.position = at;
            var ps = go.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = .5f; main.loop = false; main.startLifetime = 1.1f; main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.08f, .16f); main.gravityModifier = .5f; main.stopAction = ParticleSystemStopAction.Destroy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.45f, .8f, .35f), new Color(.8f, .95f, .5f));
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 16) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .2f;
            if (!leafMaterial) leafMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Tree leaves" };
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = leafMaterial;
            ps.Play();
        }
    }
}
