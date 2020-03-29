﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace NRP
{
    public class PasswordHolder : IDisposable, IEnumerable<(uint, char)>
    {
        private IComparer<(uint, char)> _comparer;
        private bool disposed;
        private GCHandle _handle;
        private List<(uint, char)> _list;
        private RandomNumberGenerator _rng;
        private SecureString _secStr;

        public int Length => _list.Count;

        private PasswordHolder(bool internalConst)
        {
            if (internalConst)
            {
                _comparer = new TupleComparer();
                _rng = RandomNumberGenerator.Create();
                _secStr = new SecureString();
            }
        }
        public PasswordHolder() : this(true) => _list = new List<(uint, char)>(4);
        public PasswordHolder(int capacity) : this(true) => _list = new List<(uint, char)>(capacity);

        public void AddCharacter(char character)
        {
            uint seed = this.GetSeed();
            while (seed == 0u || _list.Exists(x => x.Item1 == seed))
            {
                seed = this.GetSeed();
            }
            _list.Add((seed, character));
        }

        public string CreateAsString()
        {
            if (this.Length <= 0)
                return null;

            _list.Sort(_comparer);
            char[] chars = new char[_list.Count];
            for (int i = 0; i < _list.Count; i++)
            {
                chars[i] = _list[i].Item2;
            }

            return new string(chars);
        }
        public byte[] CreateAsByteArray()
        {
            _list.Sort(_comparer);
            _list.ForEach((x) =>
            {
                _secStr.AppendChar(x.Item2);
            });

            byte[] plainText = null;
            _handle = GCHandle.Alloc(plainText, GCHandleType.Pinned);

            IntPtr unManagedPtr = IntPtr.Zero;

            SecureStringToByteArray(unManagedPtr, _secStr, ref plainText);
            return plainText;
        }

        public void ClearPasswordFromMemory(IntPtr ptr, ref byte[] plainText)
        {
            this.Dispose();
            Array.Clear(plainText, 0, plainText.Length);
            _handle.Free();
            if (ptr != IntPtr.Zero)
                Marshal.ZeroFreeBSTR(ptr);
        }
        private static void SecureStringToByteArray(IntPtr ptr, SecureString cipherText, ref byte[] plainText)
        {
            ptr = Marshal.SecureStringToBSTR(cipherText);

            plainText = new byte[cipherText.Length];

            unsafe
            {
                // Copy without null bytes
                byte* bstr = (byte*)ptr;
                for (int i = 0; i < plainText.Length; i++)
                {
                    plainText[i] = *bstr++;
                    *bstr = *bstr++;
                }
            }
        }

        #region COMPARER
        private class TupleComparer : IComparer<(uint, char)>
        {
            public int Compare((uint, char) x, (uint, char) y) => x.Item1.CompareTo(y.Item1);
        }

        #endregion

        #region IDISPOSABLE
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                _list.Clear();
                _rng.Dispose();
                _secStr.Dispose();
                disposed = true;
            }
        }

        #endregion

        #region ENUMERATORS
        public IEnumerator<(uint, char)> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        #endregion

        #region SEED
        private uint GetSeed()
        {
            return GetSeed(_rng);
        }
        public static uint GetSeed(RandomNumberGenerator rng)
        {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

        #endregion
    }
}