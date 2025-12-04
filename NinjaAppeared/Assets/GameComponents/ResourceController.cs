using System.Collections.Generic;
using System.Linq;
using UnityEngine;



/// <summary>
/// 리소스를 미리 담아둘 컨트롤러
/// </summary>
public class ResourceController : MonoBehaviour
{
    [Header("UI - Heart")]
    [Tooltip("UI > CharacterInfoPanel > LifePanel 에 배치할 라이프 프리팹")]
    public GameObject LifeOnePrefab;
    [Tooltip("비어있는 하트 이미지")]
    public Sprite HeartEmptySprite;
    [Tooltip("반만 차있는 하트 이미지")]
    public Sprite HeartHalfSprite;
    [Tooltip("꽌찬 하트 이미지")]
    public Sprite HeartFullSprite;



    [Header("Combo System")]
    [Tooltip("콤보 프리팹")]
    public GameObject ComboPrefab;
    [Tooltip("콤보 배경으로 사용할 이미지")]
    public List<Sprite> ComboImg;


    [Tooltip("스타일 등급에 사용할 이미지")]
    public List<Sprite> StyleImg;



    [Tooltip("칼질에 사용할 애니 데이터 세트 리스트 (Sprite 배열 세트)")]
    public List<KnifeWork_ResourcesDataModel> KnifeWorkResources;


    [Header("Enemy System")]
    [Tooltip("적이 죽었을때 적용될 쉐이더")]
    public Shader DeadShader;
    [Tooltip("적 리스트 (파생 타입을 포함해 인스펙터에서 편집 가능)")]
    [SerializeReference]
    public List<EnemyDataModelBase> EnemyList = new List<EnemyDataModelBase>();


    [Header("Item System")]
    [Tooltip("아이템 프리팹")]
    public GameObject ItmePrefab;
    [Tooltip("아이템 리스트 (파생 타입을 포함해 인스펙터에서 편집 가능)")]
    [SerializeReference]
    public List<ItemDataModelBase> ItemList = new List<ItemDataModelBase>();


#if UNITY_EDITOR
    private void OnValidate()
    {
        //변경사항이 있는지 여부
        bool changed = false;


        #region 적 리스트
        if (EnemyList == null)
        {
            EnemyList = new List<EnemyDataModelBase>();
            changed = true;
        }

        // null 항목 제거
        EnemyList.RemoveAll(w => w == null);

        // 지정된 개체가 각각 정확히1개만 있도록 유지
        if (null == EnemyList.Where(w => w is AlienDataModel).FirstOrDefault())
        {
            EnemyList.Add(new AlienDataModel());
            changed = true;
        }
        if (null == EnemyList.Where(w => w is AlienJumpDataModel).FirstOrDefault())
        {
            EnemyList.Add(new AlienJumpDataModel());
            changed = true;
        }
        if (null == EnemyList.Where(w => w is AlienDuckDataModel).FirstOrDefault())
        {
            EnemyList.Add(new AlienDuckDataModel());
            changed = true;
        }
        if (null == EnemyList.Where(w => w is AlienEnemyFlyingDataModel).FirstOrDefault())
        {
            EnemyList.Add(new AlienEnemyFlyingDataModel());
            changed = true;
        }
        if (null == EnemyList.Where(w => w is AlienEnemyFlyingMiddleDataModel).FirstOrDefault())
        {
            EnemyList.Add(new AlienEnemyFlyingMiddleDataModel());
            changed = true;
        }
        #endregion


        #region 아이템 리스트
        if (ItemList == null)
        {
            ItemList = new List<ItemDataModelBase>();
            changed = true;
        }

        // null 항목 제거
        ItemList.RemoveAll(w => w == null);

        // 지정된 개체가 각각 정확히1개만 있도록 유지
        if (null == ItemList.Where(w => w is HeartItemDataModel).FirstOrDefault())
        {
            ItemList.Add(new HeartItemDataModel());
            changed = true;
        }
        if (null == ItemList.Where(w => w is StarItemDataModel).FirstOrDefault())
        {
            ItemList.Add(new StarItemDataModel());
            changed = true;
        }
        if (null == ItemList.Where(w => w is ClockItemDataModel).FirstOrDefault())
        {
            ItemList.Add(new ClockItemDataModel());
            changed = true;
        }
        #endregion


        //데이터가 갱신되면 에디터에 변경사항을 알림
        if (changed && !Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
