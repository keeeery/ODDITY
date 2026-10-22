using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Boss
{
    // 테스트용: 1 키로 투하 패턴, 2 키로 투척 패턴 실행
    // - 패턴 오브젝트를 활성화해 시작하고, 패턴이 완료되면 다시 비활성화
    public sealed class PsychokinesisPatternTester : MonoBehaviour
    {
        [SerializeField] private PsychokinesisDropPattern dropPattern;
        [SerializeField] private PsychokinesisThrowPattern throwPattern;

        private void OnEnable()
        {
            dropPattern.Completed += DeactivateDropPattern;
            throwPattern.Completed += DeactivateThrowPattern;
        }

        private void OnDisable()
        {
            dropPattern.Completed -= DeactivateDropPattern;
            throwPattern.Completed -= DeactivateThrowPattern;
        }

        private void Update()
        {
            if (dropPattern.gameObject.activeSelf || throwPattern.gameObject.activeSelf) return;

            if (SimpleInput.GetKeyDown(Key.Digit1)) dropPattern.gameObject.SetActive(true);
            else if (SimpleInput.GetKeyDown(Key.Digit2)) throwPattern.gameObject.SetActive(true);
        }

        private void DeactivateDropPattern() => dropPattern.gameObject.SetActive(false);

        private void DeactivateThrowPattern() => throwPattern.gameObject.SetActive(false);
    }
}
