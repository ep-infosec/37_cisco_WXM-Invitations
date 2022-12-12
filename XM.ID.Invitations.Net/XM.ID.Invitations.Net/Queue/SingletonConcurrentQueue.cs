using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace XM.ID.Invitations.Net
{
    public class SingletonConcurrentQueue<T> : ConcurrentQueue<T>, IBatchingQueue<T>
    {

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static SingletonConcurrentQueue() { }

        private SingletonConcurrentQueue() { }

        public static SingletonConcurrentQueue<T> Instance { get; } = new SingletonConcurrentQueue<T>();

        public void Insert(T item)
        {
            Instance.Enqueue(item);
        }
    }
}
