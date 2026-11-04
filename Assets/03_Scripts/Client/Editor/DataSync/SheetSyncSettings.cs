using System;
using System.Collections.Generic;
using UnityEngine;

namespace VaroGO.Client.EditorTools.DataSync
{
    // 구글 시트 → ScriptableObject 동기화 설정 (에디터 전용). 프로젝트에 1개만 둔다.
    // 시트는 "링크가 있는 모든 사용자: 보기"로 공유돼 있어야 키 없이 CSV를 받을 수 있다.
    [CreateAssetMenu(menuName = "VaroGO/Data/Sheet Sync Settings")]
    public class SheetSyncSettings : ScriptableObject
    {
        [Tooltip("시트 주소 docs.google.com/spreadsheets/d/<이 부분>/edit")]
        public string spreadsheetId;

        [Tooltip("탭 하나 받는 데 기다리는 최대 시간(초). 넘기면 이전 값으로 Play")]
        [Min(0.5f)] public float timeoutSeconds = 3f;

        public List<Table> tables = new List<Table>();

        [Serializable]
        public class Table
        {
            [Tooltip("로그에 표시할 이름 (보통 시트 탭 이름)")]
            public string name;

            [Tooltip("시트에서 탭을 클릭했을 때 주소 끝 #gid=<숫자>. 첫 탭도 0이 아닐 수 있으니 꼭 주소에서 복사")]
            public string gid = "0";

            [Tooltip("비우면 위 spreadsheetId를 쓴다. 다른 시트 파일이면 그 파일 ID")]
            public string spreadsheetId;

            [Tooltip("key / value 시트가 값을 넣을 에셋.\n비우면 '프리팹 시트'로 읽는다: prefab / child / component / key / value 열")]
            public ScriptableObject target;

            [Tooltip("끄면 이 탭은 건너뛴다")]
            public bool enabled = true;
        }

        public string IdOf(Table t) => string.IsNullOrWhiteSpace(t.spreadsheetId) ? spreadsheetId : t.spreadsheetId.Trim();

        public string CsvUrl(Table t) =>
            $"https://docs.google.com/spreadsheets/d/{IdOf(t)}/export?format=csv&gid={t.gid}";

        public string EditUrl(Table t) =>
            $"https://docs.google.com/spreadsheets/d/{IdOf(t)}/edit#gid={t.gid}";
    }
}
