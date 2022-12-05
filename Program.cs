using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace AdvancedSynchronization
{
    class Program
    {
        static void Main(string[] args)
        { }
    }

    public interface IStack<T>
    {
        void Push(T item);
        bool TryPop(out T item);
        int Count { get; }
    }

    class ConcurrentStack<T> : IStack<T>
    {
        private class Node
        {
            public readonly T Value;
            internal Node Next;
            
            internal Node(T value)
            {
                Value = value;
                Next = null;
            }
        }

        private Node head;

        public void Push(T item)
        {
            var spinWait = new SpinWait();
            while (true)
            {
                var node = new Node(item) { Next = head };
                if (Interlocked.CompareExchange(ref head, node, node.Next) == node.Next)
                    break;
                spinWait.SpinOnce();
            }
        }

        public bool TryPop(out T item)
        {
            var spinWait = new SpinWait();
            while (true)
            {
                var outHead = head;
                if (outHead == null)
                {
                    item = default;
                    return false;
                }
                if (Interlocked.CompareExchange(ref head, outHead.Next, outHead) == outHead)
                {
                    item = outHead.Value;
                    return true;
                }
                spinWait.SpinOnce();
            }
        }

        public int Count
        {
            get
            {
                var count = 0;
                for (var e = head; e.Next != null; e  = e.Next)
                    count++;
                return count;
            }
        } 

}
