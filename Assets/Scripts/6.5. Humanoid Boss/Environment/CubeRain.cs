using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using TeamOdd.Boss;
using TeamOdd.Combat;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamOdd.Environment
{
    public class CubeRain : MonoBehaviour
    {
        [Title("References")]
        [SerializeField] private GameObject[] prefabs;
        [SerializeField] private Transform root;
        [SerializeField] private CombatEntity boss;
        
        [Title("Settings")]
        [SerializeField] private float minCycleTime = 1f;
        [SerializeField] private float maxCycleTime = 2f;

        private int maxIndex;
        private readonly Dictionary<int, Queue<GameObject>> queueMap = new();

        private void Awake()
        {
            maxIndex = prefabs.Length;

            for (int i = 0; i < prefabs.Length; i++)
                queueMap[i] = new Queue<GameObject>();
        }

        private async UniTaskVoid Execute()
        {
            while (true)
            {
                if (boss.IsDead) break;

                float cycleTime = Random.Range(minCycleTime, maxCycleTime);
                int randIndex = Random.Range(0, maxIndex + 1);

                GameObject obj = Dequeue(randIndex);
            }
        }

        #region OBJECT_POOLING

        private void Enqueue(int idx, GameObject obj)
        {
            if (idx < 0) return;
            if (idx > maxIndex) return;
            if (obj == null) return;
            
            queueMap[idx].Enqueue(obj);
        }

        private GameObject Dequeue(int idx)
        {
            if (idx < 0) return null;
            if (idx > maxIndex) return null;
            
            Queue<GameObject> queue = queueMap[idx];

            if (queue.TryDequeue(out GameObject result))
                return result;

            GameObject obj = Instantiate(prefabs[idx], root);
            obj.SetActive(false);
            
            return obj;
        }

        #endregion
    }
}