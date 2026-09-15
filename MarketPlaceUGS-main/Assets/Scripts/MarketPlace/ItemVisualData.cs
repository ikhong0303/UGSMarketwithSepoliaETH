using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName ="ItemVisualData", menuName = "Market/ItemVisualData")]
public class ItemVisualData : ScriptableObject {


    [Serializable]
    public class ItemMapping {
        public string id;   // USG resouce ID
        public string itemName;
        public int price;
        public Sprite icon;
    }

    [Header("ID to Visual Mapping")]
    public List<ItemMapping> items = new List<ItemMapping>();

    // ID로 전체 매핑 데이터를 찾는 함수 (이미지와 이름 둘 다 쓰기 위해)
    public ItemMapping GetMapping(string id) {
        if (string.IsNullOrEmpty(id)) return null;

        foreach(var item in items) {
            if (item.id == id) return item;
        }
        return null;
    }
}
