using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace MGT.Utilities.Collections
{
    public class CircularBuffer<T> : IEnumerable<T>
    {
        private T[] _buffer;

        private int _latestIndex = -1;
        private bool bufferFull = false;

        public int BufferSize { get; private set; }

        public int Count
        {
            get
            {
                if (bufferFull)
                    return BufferSize;
                else
                    return _latestIndex + 1;
            }
        }

        public CircularBuffer(int size)
        {
            BufferSize = size;
            _latestIndex = -1;
            _buffer = new T[BufferSize];
        }

        public void Add(T item)
        {
            _latestIndex++;

            if (_latestIndex == BufferSize)
            {
                bufferFull = true;
                _latestIndex = 0;
            }

            _buffer[_latestIndex] = item;
        }

        public void Clear()
        {
            _buffer = new T[BufferSize];
            _latestIndex = -1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_latestIndex < 0)
                yield break;

            int currentIndex = _latestIndex;
            int loopCounter = 0;
            while (loopCounter != BufferSize)
            {
                loopCounter++;
                yield return _buffer[currentIndex];

                currentIndex--;
                if (currentIndex < 0)
                {
                    if (bufferFull)
                        currentIndex = BufferSize - 1;
                    else
                        yield break;
                }
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T this[int i]
        {
            get
            {
                if (i >= BufferSize)
                    throw new ArgumentOutOfRangeException();

                int index = _latestIndex - i;
                if (index < 0)
                    index = BufferSize + index;
                return _buffer[index];
            }
            set
            {
                if (i >= BufferSize)
                    throw new ArgumentOutOfRangeException();

                int index = _latestIndex - i;
                if (index < 0)
                    index = BufferSize - index;
                _buffer[index] = value;
            }
        }

        public T[] ToArray()
        {
            if (_latestIndex < 0)
                return _buffer;

            T[] result = new T[BufferSize];

            for (int i = 0; i < result.Length; i++)
            {
                int index = _latestIndex - i;
                if (index < 0)
                    index = _buffer.Length + index;

                result[i] = _buffer[index];
            }

            return result;
        }
    }
}
