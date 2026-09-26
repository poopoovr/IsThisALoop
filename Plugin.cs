using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace IsThisALoop
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public partial class Main : BaseUnityPlugin
    {
        public class Snapshot
        {
            public float time;
            public Vector3[] bPos;
            public Quaternion[] bRot;
        }

        private class CloneData
        {
            public GameObject root;
            public Transform[] cloneBones;
            public List<Collider> allColliders = new List<Collider>();
            public float delay;
            public float spawnTime;
        }

        private Snapshot[] logBuffer;
        private int head = 0;
        private int count = 0;
        private Transform[] sourceBones;
        private bool initialized = false;
        private const int BUFFER_SIZE = 27000;

        private List<CloneData> clones = new List<CloneData>();
        
        public static float interval = 20f;
        public static int maxClones = 5;
        public static bool collisionEnabled = true;
        public static bool agedClones = false;
        public static bool infiniteClones = false;
        public static bool isEnabled = true;

        private float timer = 0f;

        private List<CloneData> frozenClones = new List<CloneData>();
        private bool wasFreezePressed = false;

        private void Start()
        {
        }

        public void ToggleMod()
        {
            isEnabled = !isEnabled;
            if (!isEnabled)
            {
                ClearClones();
                initialized = false;
                sourceBones = null;
                logBuffer = null;
            }
        }

        public void ClearClones()
        {
            foreach (CloneData c in clones)
            {
                if (c.root != null)
                {
                    foreach (Collider col in c.allColliders)
                        if (col != null) col.enabled = false;
                    Destroy(c.root);
                }
            }
            clones.Clear();

            foreach (CloneData c in frozenClones)
            {
                if (c.root != null)
                {
                    foreach (Collider col in c.allColliders)
                        if (col != null) col.enabled = false;
                    Destroy(c.root);
                }
            }
            frozenClones.Clear();

            timer = 0f;
            count = 0;
            head = 0;
        }

        private void Update()
        {
            if (initialized && sourceBones != null && (sourceBones[0] == null || sourceBones[0].gameObject == null))
            {
                ClearClones();
                initialized = false;
                sourceBones = null;
                logBuffer = null;
            }

            if (!isEnabled) return;

            RecordFrame();

            bool freezePressed = ControllerInputPoller.instance.rightControllerSecondaryButton || BepInEx.UnityInput.Current.GetKeyDown(KeyCode.B);
            if (freezePressed && !wasFreezePressed) SpawnFrozenClone();
            wasFreezePressed = freezePressed;
            
            if (initialized)
            {
                timer += Time.deltaTime;
                int limit = infiniteClones ? 999 : maxClones;
                if (timer >= interval && clones.Count < limit)
                {
                    timer = 0f;
                    SpawnClone((clones.Count + 1) * interval);
                }
            }

            UpdateClones();
            UpdateColliders();
        }

        private GameObject CloneRig()
        {
            GameObject clone = Instantiate(GorillaTagger.Instance.offlineVRRig.gameObject);
            
            Component[] comps = clone.GetComponentsInChildren<Component>();
            foreach (Component comp in comps)
            {
                if (comp is Transform || comp is Renderer || comp is MeshFilter || comp is Collider)
                    continue;
                Destroy(comp);
            }

            Transform[] allTransforms = clone.GetComponentsInChildren<Transform>();
            foreach (Transform t in allTransforms)
                t.gameObject.layer = 0;
            clone.layer = 0;

            return clone;
        }

        private void SetupColliders(CloneData c)
        {
            Collider[] existing = c.root.GetComponentsInChildren<Collider>();
            foreach (Collider col in existing)
            {
                col.gameObject.layer = 9;
                col.enabled = false;
                c.allColliders.Add(col);
            }
        }

        private void SpawnClone(float delayAmount)
        {
            CloneData c = new CloneData();
            c.delay = delayAmount;
            c.spawnTime = Time.time;
            c.root = CloneRig();
            c.root.name = "GhostClone";
            c.cloneBones = c.root.GetComponentsInChildren<Transform>();
            SetupColliders(c);
            clones.Add(c);
        }

        private void SpawnFrozenClone()
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null) return;
            
            CloneData c = new CloneData();
            c.spawnTime = Time.time;
            c.root = CloneRig();
            c.root.name = "FrozenClone";
            c.cloneBones = c.root.GetComponentsInChildren<Transform>();
            SetupColliders(c);
            frozenClones.Add(c);
        }

        private void RecordFrame()
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null) return;
            
            Transform[] currentBones = GorillaTagger.Instance.offlineVRRig.GetComponentsInChildren<Transform>();
            
            if (!initialized || sourceBones == null || sourceBones.Length != currentBones.Length)
            {
                sourceBones = currentBones;
                
                logBuffer = new Snapshot[BUFFER_SIZE];
                for (int i = 0; i < BUFFER_SIZE; i++)
                {
                    logBuffer[i] = new Snapshot 
                    { 
                        bPos = new Vector3[sourceBones.Length],
                        bRot = new Quaternion[sourceBones.Length]
                    };
                }
                ClearClones();
                initialized = true;
            }

            Snapshot snap = logBuffer[head];
            snap.time = Time.time;
            
            for (int i = 0; i < sourceBones.Length; i++)
            {
                if (i == 0) 
                {
                    snap.bPos[i] = sourceBones[i].position;
                    snap.bRot[i] = sourceBones[i].rotation;
                } 
                else 
                {
                    snap.bPos[i] = sourceBones[i].localPosition;
                    snap.bRot[i] = sourceBones[i].localRotation;
                }
            }
            
            head = (head + 1) % logBuffer.Length;
            if (count < logBuffer.Length) count++;
        }

        private void UpdateClones()
        {
            if (count == 0 || clones.Count == 0) return;

            for (int ci = 0; ci < clones.Count; ci++)
            {
                CloneData c = clones[ci];
                float target = Time.time - c.delay;
                
                Snapshot best = null;
                float minDiff = float.MaxValue;
                
                for (int i = 0; i < count; i++)
                {
                    Snapshot s = logBuffer[i];
                    float diff = Mathf.Abs(s.time - target);
                    if (diff < minDiff)
                    {
                        minDiff = diff;
                        best = s;
                    }
                }

                if (best != null && c.cloneBones != null && c.cloneBones.Length == best.bPos.Length)
                {
                    for (int i = 0; i < c.cloneBones.Length; i++)
                    {
                        if (i == 0) 
                        {
                            c.cloneBones[i].position = best.bPos[i];
                            c.cloneBones[i].rotation = best.bRot[i];
                        } 
                        else 
                        {
                            c.cloneBones[i].localPosition = best.bPos[i];
                            c.cloneBones[i].localRotation = best.bRot[i];
                        }
                    }
                }

                if (agedClones)
                {
                    float ageRatio = (float)ci / clones.Count;
                    Color ageColor = Color.Lerp(Color.white, Color.red, ageRatio);
                    foreach (Renderer r in c.root.GetComponentsInChildren<Renderer>())
                    {
                        if (r.material != null) r.material.color = ageColor;
                    }
                }
            }
        }

        private void UpdateColliders()
        {
            if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null) return;

            Vector3 playerPos = GorillaTagger.Instance.offlineVRRig.transform.position;

            foreach (CloneData c in clones)
            {
                bool on = collisionEnabled;
                
                float age = Time.time - c.spawnTime;
                if (age < c.delay + 1f) on = false;
                
                if (on && c.root != null)
                {
                    float dist = Vector3.Distance(playerPos, c.root.transform.position);
                    if (dist < 0.8f) on = false;
                }
                foreach (Collider col in c.allColliders)
                    if (col != null) col.enabled = on;
            }
            
            foreach (CloneData c in frozenClones)
            {
                bool on = collisionEnabled;

                float age = Time.time - c.spawnTime;
                if (age < 1f) on = false;

                if (on && c.root != null)
                {
                    float dist = Vector3.Distance(playerPos, c.root.transform.position);
                    if (dist < 0.8f) on = false;
                }
                foreach (Collider col in c.allColliders)
                    if (col != null) col.enabled = on;
            }
        }
    }
}
