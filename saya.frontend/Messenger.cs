using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace saya.frontend
{
    public class Messenger
    {
        private class WeakAction
        {
            public WeakReference Reference { get; }
            public MethodInfo Method { get; }

            public WeakAction(object target, MethodInfo method)
            {
                Reference = new WeakReference(target);
                Method = method;
            }

            public void Invoke(params object[] arguments)
            {
                if (Reference.IsAlive)
                {
                    Method.Invoke(Reference.Target, arguments);
                }
            }
        }

        public static readonly Messenger Default = new Messenger();

        private ConcurrentDictionary<Type, List<WeakAction>> MessageListenres = new ConcurrentDictionary<Type, List<WeakAction>>();
        private ReaderWriterLockSlim listLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public void Register<TMessage>(Action<TMessage> action)
        {
            var messageType = typeof(TMessage);
            var weakActions = new List<WeakAction> { new WeakAction(action.Target, action.Method), };
            MessageListenres.AddOrUpdate(messageType, weakActions, (t, lists) =>
            {
                listLock.EnterWriteLock();
                try
                {
                    if (lists != weakActions)
                    {
                        lists.AddRange(weakActions);
                    }
                    return lists;
                }
                finally
                {
                    listLock.ExitWriteLock();
                }
            });
        }

        public void Send<TMessage>(TMessage message)
        {
            var messageType = typeof(TMessage);

            List<WeakAction> weakActions;
            if (MessageListenres.TryGetValue(messageType, out weakActions))
            {
                listLock.EnterReadLock();
                try
                {
                    foreach (var weakAction in weakActions)
                    {
                        weakAction.Invoke(message);
                    }
                }
                finally
                {
                    listLock.ExitReadLock();
                }
            }
        }
    }
}
