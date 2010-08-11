﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace BioLink.Data {

    public class MatrixColumn {
        public string Name { get; set; }
    }

    public class VirtualMatrixColumn : MatrixColumn {
        
        public System.Func<MatrixRow, Object> ValueGenerator { get; set; }

        public object GetValue(MatrixRow row) {
            if (ValueGenerator != null) {
                return ValueGenerator(row);
            }
            return null;
        }
    }

    public class DataMatrix : IEnumerable {

        public DataMatrix() {
            this.Columns = new List<MatrixColumn>();
            this.Rows = new List<MatrixRow>();
        }

        public List<MatrixColumn> Columns { get; set; }
        public List<MatrixRow> Rows { get; set; }

        public MatrixRow AddRow() {
            MatrixRow row = new MatrixRow(this, new object[Columns.Count]);
            Rows.Add(row);
            return row;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new GenericEnumerator(Rows.ToArray());
        }

        public int IndexOf(string columnName) {
            int index = 0;
            foreach (MatrixColumn col in Columns) {
                if (col.Name == columnName) {
                    return index;
                }
                index++;
            }
            return -1;
        }
    }

    public class MatrixRow {

        private object[] _data;
        private DataMatrix _matrix;

        internal MatrixRow(DataMatrix matrix, object[] data) {
            _matrix = matrix;
            _data = data;
        }

        public Object this [int index] {
            get {
                if (index >= _data.Length) {
                    // This might be a virtual column...
                    if (index < _matrix.Columns.Count) {
                        VirtualMatrixColumn vcol = _matrix.Columns[index] as VirtualMatrixColumn;
                        if (vcol != null) {
                            return vcol.GetValue(this);
                        }                            
                    }
                    // If we get here, something bad has happened!
                    throw new IndexOutOfRangeException();
                }

                return _data[index];
            }
            set { _data[index] = value; }
        }

        public int Count {
            get { return _matrix.Columns.Count; }
        }

        public object First(Predicate<object> predicate) {
            for (int i = 0; i < _matrix.Columns.Count; ++i) {
                object val = this[i];
                if (predicate(val)) {
                    return val;
                }
            }
            return null;
        }

        public void ForEach(System.Func<object, bool> func) {
            for (int i = 0; i < _matrix.Columns.Count; ++i) {
                object val = this[i];
                if (!func(val)) {
                    break;
                }
            }
        }


        public DataMatrix Matrix { 
            get { return _matrix; } 
        }

    }

    class GenericEnumerator : IEnumerator {
        private readonly object[] _list;
        // Enumerators are positioned before the first element
        // until the first MoveNext() call. 

        private int _position = -1;

        public GenericEnumerator(object[] list) {
            _list = list;
        }

        public bool MoveNext() {
            _position++;
            return (_position < _list.Length);
        }

        public void Reset() {
            _position = -1;
        }

        public object Current {
            get {
                try { return _list[_position]; } catch (IndexOutOfRangeException) { throw new InvalidOperationException(); }
            }
        }
    }

}