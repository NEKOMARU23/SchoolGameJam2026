using System;
using System.Collections.Generic;


/// <summary>
/// イベントの送信と受信を仲介する管理クラス
/// </summary>

namespace TechC.SchoolGameJam2026.Core.Event
{
    /// <summary>
    /// イベントの送信と受信を仲介する管理クラス
    /// </summary>
    public static class EventManager
    {
        // イベント型に対応するDelegateを管理する辞書
        private static readonly Dictionary<Type, Delegate> eventTable = new Dictionary<Type, Delegate>();

        /// <summary>
        /// イベントの購読登録（受け取るクラスが呼び出す）
        /// </summary>
        public static void Subscribe<T>(Action<T> listener)
        {
            Type eventType = typeof(T);

            if (!eventTable.ContainsKey(eventType))
            {
                eventTable[eventType] = null;
            }

            eventTable[eventType] = (Action<T>)eventTable[eventType] + listener;
        }

        /// <summary>
        /// イベントの購読解除（オブジェクト破棄時などに呼び出す）
        /// </summary>
        public static void Unsubscribe<T>(Action<T> listener)
        {
            Type eventType = typeof(T);

            if (eventTable.ContainsKey(eventType) && eventTable[eventType] != null)
            {
                eventTable[eventType] = (Action<T>)eventTable[eventType] - listener;

                if (eventTable[eventType] == null)
                {
                    eventTable.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// イベントの発火（通知を送るクラスが呼び出す）
        /// </summary>
        public static void Publish<T>(T eventData)
        {
            Type eventType = typeof(T);

            if (eventTable.TryGetValue(eventType, out Delegate del))
            {
                (del as Action<T>)?.Invoke(eventData);
            }
        }

        /// <summary>
        /// 全イベントのクリア（シーン遷移時など）
        /// </summary>
        public static void Clear()
        {
            eventTable.Clear();
        }
    }
}
