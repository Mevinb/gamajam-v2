using Parasite.World;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Characters
{
    /// <summary>Unpossessed behaviour: wander / patrol / stand at post, and stop to stare at suspicious things.</summary>
    public class HostBrain : MonoBehaviour
    {
        public NpcBehaviour behaviour = NpcBehaviour.Wander;
        public PatrolRoute route;
        public float wanderRadius = 14f;

        protected HostCharacter host;
        protected Vector3 home;
        protected Quaternion homeRot;
        int waypoint;
        float waitT, repathT, noticeT, idleLookT;
        Vector3 noticePoint;

        /// <summary>' ' none, '?' curious, '!' alarmed. Shown above heads by the HUD.</summary>
        public char AlertIcon { get; protected set; } = ' ';
        protected float alertIconT;

        public virtual void Setup(HostCharacter h, NpcSpawn spawn)
        {
            host = h;
            home = transform.position; homeRot = transform.rotation;
            if (spawn)
            {
                behaviour = spawn.behaviour; route = spawn.route; wanderRadius = spawn.wanderRadius;
                if (behaviour == NpcBehaviour.Patrol && (!route || route.Count == 0)) behaviour = NpcBehaviour.Wander;
            }
            waitT = Random.Range(0f, 2f);
        }

        protected bool NavReady => host && host.Nav && host.Nav.enabled && host.Nav.isOnNavMesh;

        protected void GoTo(Vector3 p, float speed)
        {
            if (!NavReady) return;
            host.Nav.speed = speed;
            host.Nav.isStopped = false;
            if ((host.Nav.destination - p).sqrMagnitude > 0.25f) host.Nav.SetDestination(p);
        }

        protected void Stop()
        {
            if (!NavReady) return;
            host.Nav.isStopped = true;
            host.Nav.velocity = Vector3.zero;
        }

        protected bool Arrived(float tol = 0.6f) =>
            NavReady && !host.Nav.pathPending && host.Nav.remainingDistance <= Mathf.Max(tol, host.Nav.stoppingDistance);

        protected void Face(Vector3 point, float dt, float speed = 6f)
        {
            var d = point - transform.position; d.y = 0;
            if (d.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-speed * dt));
        }

        /// <summary>Stop and stare at something for a while.</summary>
        public virtual void Notice(Vector3 point, float seconds, char icon = '?')
        {
            noticePoint = point;
            noticeT = Mathf.Max(noticeT, seconds);
            SetIcon(icon, seconds);
            if (icon == '!' && host && host.Profession != Profession.Guard && fleeT <= 0f && NavReady)
            {
                Vector3 away = transform.position - point; away.y = 0;
                if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                var dest = transform.position + away.normalized * 9f;
                if (NavMesh.SamplePosition(dest, out var hit, 4f, NavMesh.AllAreas)) { fleeDest = hit.position; fleeT = 3.5f; noticeT = 0f; }
            }
        }

        float fleeT;
        Vector3 fleeDest;

        protected void SetIcon(char c, float seconds)
        {
            if (c == '!' || AlertIcon != '!' || alertIconT <= 0f) { AlertIcon = c; alertIconT = seconds; }
        }

        public virtual void OnPossessed() { noticeT = 0f; fleeT = 0f; alertIconT = 0f; AlertIcon = ' '; host.Rig.LookAt(null); }
        public virtual void OnRecovered()
        {
            Notice(transform.position + transform.forward * 2f, 1.5f, '?');
            waitT = 1f;
        }

        public virtual void Tick(float dt)
        {
            if (alertIconT > 0f) { alertIconT -= dt; if (alertIconT <= 0f) AlertIcon = ' '; }
            if (fleeT > 0f)
            {
                fleeT -= dt;
                GoTo(fleeDest, host.Profile.runSpeed);
                host.Rig.LookAt(null);
                if (fleeT <= 0f) { noticeT = 1.5f; }
                return;
            }
            if (noticeT > 0f)
            {
                noticeT -= dt;
                Stop();
                Face(noticePoint, dt);
                host.Rig.LookAt(noticePoint + Vector3.up * 0.5f);
                return;
            }
            host.Rig.LookAt(null);
            if (RoutineOverride != null) RoutineOverride(dt);
            else Routine(dt);
        }

        // ---------- mission scripting (THE TAIL / HUNTER PROTOCOL): replaces only the idle routine; notice/flee still apply
        /// <summary>When set, called instead of the Wander/Patrol/Stationary routine while this NPC is awake and calm.</summary>
        public System.Action<float> RoutineOverride;
        public void ScriptMove(Vector3 p, float speed) => GoTo(p, speed);
        public void ScriptStop() => Stop();
        public bool ScriptArrived(float tolerance = 0.6f) => Arrived(tolerance);
        public bool ScriptNavReady => NavReady;
        public void ScriptFace(Vector3 point, float dt, float speed = 6f) => Face(point, dt, speed);
        public void ShowAlert(char icon, float seconds) => SetIcon(icon, seconds);
        public void SetHome(Vector3 position, Quaternion rotation) { home = position; homeRot = rotation; }

        protected void Routine(float dt)
        {
            float walk = host.Profile.walkSpeed;
            switch (behaviour)
            {
                case NpcBehaviour.Stationary:
                    if ((transform.position - home).sqrMagnitude > 1f) GoTo(home, walk);
                    else
                    {
                        Stop();
                        idleLookT -= dt;
                        var fwd = homeRot * Vector3.forward;
                        if (idleLookT < -3f) idleLookT = Random.Range(3f, 7f);
                        var look = idleLookT < 0f ? Quaternion.Euler(0, 50f, 0) * fwd : fwd;
                        Face(transform.position + look, dt, 3f);
                    }
                    break;
                case NpcBehaviour.Patrol:
                    if (waitT > 0f) { waitT -= dt; Stop(); break; }
                    GoTo(route.Point(waypoint), walk);
                    if (Arrived())
                    {
                        waitT = route.waitAtPoint;
                        waypoint = route.loop ? (waypoint + 1) % route.Count : Mathf.Min(waypoint + 1, route.Count - 1);
                    }
                    break;
                default:
                    if (waitT > 0f) { waitT -= dt; Stop(); break; }
                    repathT -= dt;
                    if (repathT <= 0f || Arrived())
                    {
                        if (repathT <= 0f || Random.value < 0.5f)
                        {
                            var p = home + Random.insideUnitSphere * wanderRadius; p.y = home.y;
                            if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) GoTo(hit.position, walk);
                            repathT = Random.Range(8f, 16f);
                        }
                        else { waitT = Random.Range(1.5f, 4.5f); repathT = 0.5f; }
                    }
                    break;
            }
        }
    }
}
