using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechC.SchoolGameJam2026.Scripts.Core.Event
{
    /// <summary>
    /// 型ベースのイベント送信・受信を仲介する静的管理クラス (Pub/Subパターン)
    /// Rule 10 準拠
    /// </summary>
    public static class EventManager
    {
        // イベント型に対応するDelegateを管理する辞書
        private static readonly Dictionary<Type, Delegate> eventTable = new Dictionary<Type, Delegate>();

        /// <summary>
        /// イベントの購読登録
        /// </summary>
        /// <typeparam name="T">イベント構造体またはクラス</typeparam>
        /// <param name="listener">実行するコールバック</param>
        public static void Subscribe<T>(Action<T> listener)
        {
            if (listener == null) return;

            Type eventType = typeof(T);

            if (!eventTable.TryGetValue(eventType, out Delegate currentDelegate))
            {
                eventTable[eventType] = listener;
            }
            else
            {
                // 多重登録を防ぎたい場合は事前に Remove してから加算する
                var targetDelegate = (Action<T>)currentDelegate;
                targetDelegate -= listener; // 重複登録防止
                targetDelegate += listener;
                eventTable[eventType] = targetDelegate;
            }
        }

        /// <summary>
        /// イベントの購読解除
        /// </summary>
        /// <typeparam name="T">イベント構造体またはクラス</typeparam>
        /// <param name="listener">解除するコールバック</param>
        public static void Unsubscribe<T>(Action<T> listener)
        {
            if (listener == null) return;

            Type eventType = typeof(T);

            if (eventTable.TryGetValue(eventType, out Delegate currentDelegate) && currentDelegate != null)
            {
                var targetDelegate = (Action<T>)currentDelegate;
                targetDelegate -= listener;

                if (targetDelegate == null)
                {
                    eventTable.Remove(eventType);
                }
                else
                {
                    eventTable[eventType] = targetDelegate;
                }
            }
        }

        /// <summary>
        /// イベントの発火（通知）
        /// </summary>
        /// <typeparam name="T">イベント構造体またはクラス</typeparam>
        /// <param name="eventData">通知するデータ</param>
        public static void Publish<T>(T eventData)
        {
            Type eventType = typeof(T);

            if (eventTable.TryGetValue(eventType, out Delegate del))
            {
                if (del is Action<T> action)
                {
                    action.Invoke(eventData);
                }
            }
        }

        /// <summary>
        /// 全イベントのクリア（シーン遷移時やゲームリセット時に呼び出す）
        /// </summary>
        public static void Clear()
        {
            eventTable.Clear();
        }
    }
}