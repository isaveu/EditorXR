﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.EditorVR.Core;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Modules
{
	sealed class SerializedPreferencesModule : MonoBehaviour, IInterfaceConnector
	{
		List<ISerializePreferences> m_Serializers = new List<ISerializePreferences>();
		SerializedPreferences m_Preferences;

		[Serializable]
		class SerializedPreferences : ISerializationCallbackReceiver
		{
			[SerializeField]
			SerializedPreferenceItem[] m_Items;

			readonly Dictionary<Type, SerializedPreferenceItem> m_ItemDictionary = new Dictionary<Type, SerializedPreferenceItem>();

			public Dictionary<Type, SerializedPreferenceItem> items { get { return m_ItemDictionary; } }

			public void OnBeforeSerialize()
			{
				m_Items = m_ItemDictionary.Values.ToArray();
			}

			public void OnAfterDeserialize()
			{
				foreach (var item in m_Items)
				{
					var type = Type.GetType(item.name);
					if (type != null)
					{
						if (m_ItemDictionary.ContainsKey(type))
							Debug.LogWarning("Multiple payloads of the same type on deserialization");

						m_ItemDictionary[type] = item;
					}
				}
			}
		}

		[Serializable]
		class SerializedPreferenceItem
		{
			[SerializeField]
			string m_Name;
			[SerializeField]
			string m_PayloadType;
			[SerializeField]
			string m_Payload;

			public string name { get { return m_Name; } set { m_Name = value; } }
			public string payloadType { get { return m_PayloadType; } set { m_PayloadType = value; } }
			public string payload { get { return m_Payload; } set { m_Payload = value; } }
		}

		public void ConnectInterface(object obj, Transform rayOrigin = null)
		{
			var serializer = obj as ISerializePreferences;
			if (serializer != null)
				m_Serializers.Add(serializer);
		}

		public void DisconnectInterface(object obj, Transform rayOrigin = null)
		{
			var serializer = obj as ISerializePreferences;
			if (serializer != null)
				m_Serializers.Remove(serializer);
		}

		internal void DeserializePreferences(string serializedPreferences)
		{
			var preferences = JsonUtility.FromJson<SerializedPreferences>(serializedPreferences);
			if (preferences != null)
			{
				m_Preferences = preferences;

				foreach (var serializer in m_Serializers)
				{
					SerializedPreferenceItem item;
					if (m_Preferences.items.TryGetValue(serializer.GetType(), out item))
					{
						var payload = JsonUtility.FromJson(item.payload, Type.GetType(item.payloadType));
						serializer.OnDeserializePreferences(payload);
					}
				}
			}
		}

		internal string SerializePreferences()
		{
			if (m_Preferences == null)
				m_Preferences = new SerializedPreferences();

			foreach (var serializer in m_Serializers)
			{
				var payload = serializer.OnSerializePreferences();

				if (payload == null)
					continue;

				var type = serializer.GetType();

				if (m_Preferences.items.ContainsKey(type))
					Debug.LogWarning("Multiple payloads of the same type on serialization");

				m_Preferences.items[type] = new SerializedPreferenceItem
				{
					name = type.FullName,
					payloadType = payload.GetType().FullName,
					payload = JsonUtility.ToJson(payload)
				};
			}

			return JsonUtility.ToJson(m_Preferences);
		}
	}
}
#endif
