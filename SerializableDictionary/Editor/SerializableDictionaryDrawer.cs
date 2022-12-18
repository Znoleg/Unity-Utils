using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Editor {
	[CustomPropertyDrawer(typeof(SerializableDictionaryBase<,,>), true)]
	public class SerializableDictionaryDrawer : PropertyDrawer {
		private SerializedProperty _property;
		private SerializedProperty _keys;
		private SerializedProperty _values;
		private SerializedProperty _beforeSerializeIgnore;
		private SerializedProperty _afterDeserializeIgnore;
		private SerializedProperty _callDeserializeAdded;
		
		private ReorderableList _list;
		private GUIContent _label;

		private bool IsExpanded {
			get => _property.isExpanded;
			set => _property.isExpanded = value;
		}

		private bool IsAligned => _keys.arraySize == _values.arraySize;
		private static float SingleLineHeight => EditorGUIUtility.singleLineHeight;

		private bool BeforeSerializeIgnore {
			get => _beforeSerializeIgnore.boolValue;
			set => _beforeSerializeIgnore.boolValue = value;
		}

		private bool AfterDeserializeIgnore {
			get => _afterDeserializeIgnore.boolValue;
			set => _afterDeserializeIgnore.boolValue = value;
		}

		private bool CallDeserializeAdded {
			get => _callDeserializeAdded.boolValue;
			set => _callDeserializeAdded.boolValue = value;
		}
		
		
		private const float ElementHeightPadding = 6f;
		private const float ElementSpacing = 10f;
		private const float TopPadding = 5f;
		private const float BottomPadding = 5f;
		private const float AddButtonHeight = 30f;

		private void Init(SerializedProperty value) {
			if (SerializedProperty.EqualContents(value, _property)) return;
		
			_property = value;

			_keys = _property.FindPropertyRelative("m_keys");
			_values = _property.FindPropertyRelative("m_values");
			_beforeSerializeIgnore = _property.FindPropertyRelative("_beforeSerializeIgnore");
			_afterDeserializeIgnore = _property.FindPropertyRelative("_afterDeserializeIgnore");
			_callDeserializeAdded = _property.FindPropertyRelative("_callDeserializeAdded");
			BeforeSerializeIgnore = AfterDeserializeIgnore = CallDeserializeAdded = false;

			_list = new ReorderableList(_property.serializedObject, _keys, true, true, true, true) {
				drawHeaderCallback = DrawHeader, 
				onAddCallback = Add, 
				onRemoveCallback = Remove, 
				elementHeightCallback = GetElementHeight,
				drawElementCallback = DrawElement
			};

			_list.onReorderCallbackWithDetails += Reorder;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			Init(property);

			float height = TopPadding + BottomPadding;

			if (IsAligned) {
				height += IsExpanded ? _list.GetHeight() : _list.headerHeight;
			} else {
				height += SingleLineHeight;
			}

			if (BeforeSerializeIgnore) {
				height += AddButtonHeight;
			}

			return height;
		}

		public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label) {
			label.text = $" {label.text}";

			_label = label;

			Init(property);

			rect = EditorGUI.IndentedRect(rect);

			rect.y += TopPadding;
			rect.height -= TopPadding + BottomPadding;

			if (IsAligned == false) {
				DrawAlignmentWarning(ref rect);
				return;
			}

			if (IsExpanded) {
				DrawList(ref rect);
			} else {
				DrawCompleteHeader(ref rect);
			}

			if (BeforeSerializeIgnore) {
				Rect buttonRect = new Rect(rect.position.x, rect.position.y + rect.height - AddButtonHeight, rect.width, AddButtonHeight);
				
				if (CheckDuplicateKeys(out SerializedProperty duplicateProperty1, out SerializedProperty duplicateProperty2)) {
					EditorGUI.HelpBox(buttonRect, $"Есть повторяющийся ключ! Повторяются {duplicateProperty1.displayName} и {duplicateProperty2.displayName}.", MessageType.Warning);
				} else {
					if (GUI.Button(buttonRect, "Нажмите сюда чтобы добавить пару")) {
						CallDeserializeAdded = true;
					}
				}
			}
		}

		private bool CheckDuplicateKeys(out SerializedProperty duplicateProperty1, out SerializedProperty duplicateProperty2) {
			duplicateProperty1 = null;
			duplicateProperty2 = null;
			
			int count = _keys.arraySize;
			for (int i = 0; i < count; i++) {
				for (int j = i + 1; j < count; j++) {
					
					SerializedProperty property1 = _keys.GetArrayElementAtIndex(i);
					SerializedProperty property2 = _keys.GetArrayElementAtIndex(j);
					if (SerializedProperty.DataEquals(property1, property2)) {
						duplicateProperty1 = property1;
						duplicateProperty2 = property2;
						return true;
					}
				}
			}

			return false;
		}

		private void DrawList(ref Rect rect) {
			EditorGUIUtility.labelWidth = 80f;
			EditorGUIUtility.fieldWidth = 80f;

			_list.DoList(rect);
		}

		private void DrawAlignmentWarning(ref Rect rect) {
			const float width = 80f;
			const float spacing = 5f;

			rect.width -= width;

			EditorGUI.HelpBox(rect, "  Misalignment Detected", MessageType.Error);

			rect.x += rect.width + spacing;
			rect.width = width - spacing;

			if (GUI.Button(rect, "Fix")) {
				if (_keys.arraySize > _values.arraySize) {
					int difference = _keys.arraySize - _values.arraySize;

					for (int i = 0; i < difference; i++)
						_keys.DeleteArrayElementAtIndex(_keys.arraySize - 1);
				} else if (_keys.arraySize < _values.arraySize) {
					int difference = _values.arraySize - _keys.arraySize;

					for (int i = 0; i < difference; i++)
						_values.DeleteArrayElementAtIndex(_values.arraySize - 1);
				}
			}
		}

		#region Draw Header

		private void DrawHeader(Rect rect) {
			rect.x += 10f;

			IsExpanded = EditorGUI.Foldout(rect, IsExpanded, _label, true);
		}

		private void DrawCompleteHeader(ref Rect rect) {
			ReorderableList.defaultBehaviours.DrawHeaderBackground(rect);

			rect.x += 6;
			rect.y += 0;

			DrawHeader(rect);
		}

		#endregion

		private float GetElementHeight(int index) {
			SerializedProperty key = _keys.GetArrayElementAtIndex(index);
			SerializedProperty value = _values.GetArrayElementAtIndex(index);

			float kHeight = GetChildrenSingleHeight(key);
			float vHeight = GetChildrenSingleHeight(value);

			float max = Mathf.Max(kHeight, vHeight);

			if (max < SingleLineHeight) max = SingleLineHeight;

			return max + ElementHeightPadding;
		}

		#region Draw Element

		private void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
			rect.height -= ElementHeightPadding;
			rect.y += ElementHeightPadding / 2;

			Rect[] areas = Split(rect, 30f, 70f);

			DrawKey(areas[0], index);
			DrawValue(areas[1], index);
		}

		private void DrawKey(Rect rect, int index) {
			SerializedProperty property = _keys.GetArrayElementAtIndex(index);

			rect.x += ElementSpacing / 2f;
			rect.width -= ElementSpacing;

			DrawField(rect, property);
		}

		private void DrawValue(Rect rect, int index) {
			SerializedProperty property = _values.GetArrayElementAtIndex(index);

			rect.x += ElementSpacing / 2f;
			rect.width -= ElementSpacing;

			DrawField(rect, property);
		}

		private static void DrawField(Rect rect, SerializedProperty property) {
			rect.height = SingleLineHeight;

			if (IsInline(property)) {
				EditorGUI.PropertyField(rect, property, GUIContent.none);
			} else {
				rect.x += ElementSpacing / 2f;
				rect.width -= ElementSpacing;

				foreach (SerializedProperty child in IterateChildren(property)) {
					EditorGUI.PropertyField(rect, child, false);

					rect.y += SingleLineHeight + 2f;
				}
			}
		}

		#endregion

		private void Reorder(ReorderableList list, int oldIndex, int newIndex) {
			_values.MoveArrayElement(oldIndex, newIndex);
		}

		private void Add(ReorderableList list) {
			AfterDeserializeIgnore = true;
			BeforeSerializeIgnore = true;
		
			_values.InsertArrayElementAtIndex(_values.arraySize);

			ReorderableList.defaultBehaviours.DoAddButton(list);
		}

		private void Remove(ReorderableList list) {
			_values.DeleteArrayElementAtIndex(list.index);

			ReorderableList.defaultBehaviours.DoRemoveButton(list);
		}

		//Static Utility
		private static Rect[] Split(Rect source, params float[] cuts) {
			Rect[] rects = new Rect[cuts.Length];

			float x = 0f;

			for (int i = 0; i < cuts.Length; i++) {
				rects[i] = new Rect(source);

				rects[i].x += x;
				rects[i].width *= cuts[i] / 100;

				x += rects[i].width;
			}

			return rects;
		}

		private static bool IsInline(SerializedProperty property) {
			switch (property.propertyType) {
				case SerializedPropertyType.Generic:
					return property.hasVisibleChildren == false;
			}

			return true;
		}

		private static IEnumerable<SerializedProperty> IterateChildren(SerializedProperty property) {
			string path = property.propertyPath;

			property.Next(true);

			while (true) {
				yield return property;

				if (property.NextVisible(false) == false) break;
				if (property.propertyPath.StartsWith(path) == false) break;
			}
		}

		private static float GetChildrenSingleHeight(SerializedProperty property) {
			if (IsInline(property)) return SingleLineHeight;

			float height = 0f;

			foreach (SerializedProperty _ in IterateChildren(property)) {
				height += SingleLineHeight + 2f;
			}

			return height;
		}
	}
}