using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

public abstract class SerializableDictionaryBase<TKey, TValue, TValueStorage> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
	[SerializeField] private TKey[] m_keys;
	[SerializeField] private TValueStorage[] m_values;

	[SerializeField, HideInInspector] private bool _beforeSerializeIgnore;
	[SerializeField, HideInInspector] private bool _afterDeserializeIgnore;
	[SerializeField, HideInInspector] private bool _callDeserializeAdded;

	public SerializableDictionaryBase()	{	}

	public SerializableDictionaryBase(IDictionary<TKey, TValue> dict) : base(dict.Count)	{
		foreach (KeyValuePair<TKey, TValue> kvp in dict)		{
			this[kvp.Key] = kvp.Value;
		}
	}
	
	protected SerializableDictionaryBase(SerializationInfo info, StreamingContext context)
	 : base(info,context){}

	protected abstract void SetValue(TValueStorage[] storage, int i, TValue value);
	protected abstract TValue GetValue(TValueStorage[] storage, int i);

	public void CopyFrom(IDictionary<TKey, TValue> dict) {
		Clear();
		foreach (KeyValuePair<TKey, TValue> kvp in dict) {
			this[kvp.Key] = kvp.Value;
		}
	}

	public void OnAfterDeserialize() {
#if UNITY_EDITOR
		if (_afterDeserializeIgnore) return;
#endif

		if (m_keys != null && m_values != null && m_keys.Length == m_values.Length)	{
			Clear(); // Clearing dictionary
			int n = m_keys.Length;
			for (int i = 0; i < n; ++i) {
				this[m_keys[i]] = GetValue(m_values, i);
			}

			m_keys = null;
			m_values = null;
		}
	}

	public void OnBeforeSerialize() {
#if UNITY_EDITOR
		if (_callDeserializeAdded) {
			_callDeserializeAdded = false;
			DeserializeAdded();
		}
		
		if (_beforeSerializeIgnore) return;
#endif
		
		int n = Count;

		m_keys = new TKey[n];
		m_values = new TValueStorage[n];

		int i = 0;
		foreach (KeyValuePair<TKey, TValue> kvp in this) {
			m_keys[i] = kvp.Key;
			SetValue(m_values, i, kvp.Value);
			++i;
		}
	}
	
	private void DeserializeAdded() {
		_afterDeserializeIgnore = false;
		OnAfterDeserialize();
		_beforeSerializeIgnore = false;
	}
}

public class SerializableDictionary<TKey, TValue> : SerializableDictionaryBase<TKey, TValue, TValue> {
	
	public SerializableDictionary() { }

	public SerializableDictionary(IDictionary<TKey, TValue> dict) 
		: base(dict) { }

	protected SerializableDictionary(SerializationInfo info, StreamingContext context) 
		: base(info,context){}

	protected override TValue GetValue(TValue[] storage, int i) {
		return storage[i];
	}

	protected override void SetValue(TValue[] storage, int i, TValue value) {
		storage[i] = value;
	}
}

public static class SerializableDictionary
{
	public class Storage<T> {
		public T data;
	}
}

public class SerializableDictionary<TKey, TValue, TValueStorage> : SerializableDictionaryBase<TKey, TValue, TValueStorage> 
	where TValueStorage : SerializableDictionary.Storage<TValue>, new() {
	
	public SerializableDictionary() { }

	public SerializableDictionary(IDictionary<TKey, TValue> dict) 
		: base(dict) { }

	protected SerializableDictionary(SerializationInfo info, StreamingContext context) 
		: base(info,context) { }

	protected override TValue GetValue(TValueStorage[] storage, int i) {
		return storage[i].data;
	}

	protected override void SetValue(TValueStorage[] storage, int i, TValue value) {
		storage[i] = new TValueStorage();
		storage[i].data = value;
	}
}