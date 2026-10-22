using UnityEngine;

// 오브젝트 풀에서 사용할 최소 단위 인터페이스
// - FPS에서는 파티클, 데미지 표시 등에 사용
// - 인터페이스를 상속 받아 Key와 GameObject 프로퍼티를 재정의하여 사용
// - 풀로 귀환하는 함수(ReturnToPool)를 상속받은 클래스의 기능에 따라 재정의하여 사용

public interface IObjectPoolItem
{
    string Key { get; set; }
    GameObject GameObject { get; }

    void ReturnToPool();
}
