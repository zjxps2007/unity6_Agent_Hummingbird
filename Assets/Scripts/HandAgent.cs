using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class HandAgent : Agent
{
    [Header("손 설정")] public Transform wrist;
    public Transform[] fingerJoints; // 손가락 관절들 (14개 관절)
    public Transform target; // 잡을 물체
    public Transform handTip; // 손끝 위치

    [Header("물리 설정")] public float jointSpeed = 100f;
    public float maxJointAngle = 45f;
    public float grabDistance = 0.3f;

    [Header("보상 설정")] public float reachReward = 1.0f;
    public float grabReward = 5.0f;
    public float distancePenalty = -0.001f;

    private Rigidbody[] jointRigidbodies;
    private ArticulationBody[] articulationBodies;
    private Vector3 initialTargetPosition;
    private Vector3 initialHandPosition;
    private bool hasGrabbedObject = false;
    private float episodeTimer = 0f;
    private float maxEpisodeTime = 30f;

    public override void Initialize()
    {
        // 관절 Rigidbody 또는 ArticulationBody 컴포넌트들 찾기
        jointRigidbodies = new Rigidbody[fingerJoints.Length];
        articulationBodies = new ArticulationBody[fingerJoints.Length];

        for (int i = 0; i < fingerJoints.Length; i++)
        {
            jointRigidbodies[i] = fingerJoints[i].GetComponent<Rigidbody>();
            articulationBodies[i] = fingerJoints[i].GetComponent<ArticulationBody>();
        }

        initialTargetPosition = target.localPosition;
        initialHandPosition = wrist.localPosition;
    }

    public override void OnEpisodeBegin()
    {
        // 손 위치 초기화
        wrist.localPosition = initialHandPosition + new Vector3(
            Random.Range(-2f, 2f),
            Random.Range(-1f, 1f),
            Random.Range(-2f, 2f)
        );

        // 물체 위치 랜덤 설정
        target.localPosition = initialTargetPosition + new Vector3(
            Random.Range(-3f, 3f),
            Random.Range(0f, 2f),
            Random.Range(-3f, 3f)
        );

        // 손가락 관절 초기화
        ResetJoints();

        hasGrabbedObject = false;
        episodeTimer = 0f;

        // 물체 물리 초기화
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.linearVelocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;
        }
    }

    private void ResetJoints()
    {
        // 모든 관절을 초기 위치로 리셋
        foreach (var joint in fingerJoints)
        {
            joint.localRotation = Quaternion.identity;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. 손목 위치와 회전 (6개 값)
        sensor.AddObservation(wrist.localPosition);
        sensor.AddObservation(wrist.localRotation);

        // 2. 목표 물체 위치 (3개 값)
        sensor.AddObservation(target.localPosition);

        // 3. 손과 물체 간의 상대적 위치 (3개 값)
        Vector3 relativePos = target.position - handTip.position;
        sensor.AddObservation(relativePos);

        // 4. 각 손가락 관절의 회전 (14 * 4 = 56개 값)
        foreach (var joint in fingerJoints)
        {
            sensor.AddObservation(joint.localRotation);
        }

        // 5. 손과 물체 간의 거리 (1개 값)
        float distanceToTarget = Vector3.Distance(handTip.position, target.position);
        sensor.AddObservation(distanceToTarget);

        // 6. 물체 잡기 상태 (1개 값)
        sensor.AddObservation(hasGrabbedObject ? 1f : 0f);

        // 7. 에피소드 타이머 (1개 값)
        sensor.AddObservation(episodeTimer / maxEpisodeTime);

        // 총 관찰값: 6 + 3 + 3 + 56 + 1 + 1 + 1 = 71개
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        episodeTimer += Time.fixedDeltaTime;

        // 연속 액션: 손목 움직임 (6개) + 손가락 관절 (14개) = 20개 액션
        var continuousActions = actionBuffers.ContinuousActions;

        // 손목 움직임 제어 (위치 3개, 회전 3개)
        Vector3 wristMovement = new Vector3(
            Mathf.Clamp(continuousActions[0], -1f, 1f),  // X 이동 (인덱스 0에서 float 값 추출 후 Clamp)
            Mathf.Clamp(continuousActions[29], -1f, 1f),  // Y 이동 (인덱스 1)
            Mathf.Clamp(continuousActions[30], -1f, 1f)   // Z 이동 (인덱스 2)
        ) * jointSpeed * Time.fixedDeltaTime;


        Vector3 wristRotation = new Vector3(
            Mathf.Clamp(continuousActions[35], -1f, 1f),  // X 회전 (인덱스 3)
            Mathf.Clamp(continuousActions[36], -1f, 1f),  // Y 회전 (인덱스 4)
            Mathf.Clamp(continuousActions[37], -1f, 1f)   // Z 회전 (인덱스 5)
        ) * jointSpeed * Time.fixedDeltaTime;

        // 손목 위치 업데이트
        wrist.localPosition += wristMovement;
        wrist.localRotation *= Quaternion.Euler(wristRotation);

        // 손가락 관절 제어 (6번 인덱스부터 시작)
        for (int i = 0; i < fingerJoints.Length && i + 6 < continuousActions.Length; i++)
        {
            float jointAction = Mathf.Clamp(continuousActions[i + 6], -1f, 1f);  // 각 손가락 액션 값 Clamp
            ControlJoint(i, jointAction);
        }

        // 보상 계산
        CalculateRewards();

        // 에피소드 종료 조건
        CheckEpisodeEnd();
    }



    private void ControlJoint(int jointIndex, float action)
    {
        if (jointIndex >= fingerJoints.Length) return;

        Transform joint = fingerJoints[jointIndex];

        // ArticulationBody 사용 시
        if (articulationBodies[jointIndex] != null)
        {
            var articulationBody = articulationBodies[jointIndex];
            var drive = articulationBody.xDrive;
            drive.target = Mathf.Clamp(action * maxJointAngle, -maxJointAngle, maxJointAngle);
            articulationBody.xDrive = drive;
        }
        // Rigidbody 사용 시
        else if (jointRigidbodies[jointIndex] != null)
        {
            float targetAngle = Mathf.Clamp(action * maxJointAngle, -maxJointAngle, maxJointAngle);
            Quaternion targetRotation = Quaternion.AngleAxis(targetAngle, Vector3.right);
            joint.localRotation =
                Quaternion.Slerp(joint.localRotation, targetRotation, Time.fixedDeltaTime * jointSpeed);
        }
    }

    private void CalculateRewards()
    {
        float distanceToTarget = Vector3.Distance(handTip.position, target.position);

        // 거리 기반 보상 (가까울수록 좋음)
        float distanceReward = -distanceToTarget * distancePenalty;
        AddReward(distanceReward);

        // 물체에 도달했을 때 보상
        if (distanceToTarget < grabDistance && !hasGrabbedObject)
        {
            AddReward(reachReward);

            // 물체 잡기 성공 검사
            if (IsGrabbingObject())
            {
                hasGrabbedObject = true;
                AddReward(grabReward);
            }
        }

        // 시간 페널티 (너무 오래 걸리면 안됨)
        AddReward(-0.001f);
    }

    private bool IsGrabbingObject()
    {
        // 여러 손가락이 물체 주변에 있는지 확인
        int fingersNearObject = 0;
        float checkDistance = 0.2f;

        foreach (var joint in fingerJoints)
        {
            if (Vector3.Distance(joint.position, target.position) < checkDistance)
            {
                fingersNearObject++;
            }
        }

        // 3개 이상의 손가락이 물체 근처에 있으면 잡은 것으로 판정
        return fingersNearObject >= 3;
    }

    private void CheckEpisodeEnd()
    {
        // 성공: 물체를 잡았을 때
        if (hasGrabbedObject)
        {
            AddReward(10f); // 최종 성공 보상
            EndEpisode();
        }

        // 실패: 시간 초과
        if (episodeTimer > maxEpisodeTime)
        {
            AddReward(-1f); // 시간 초과 페널티
            EndEpisode();
        }

        // 실패: 손이 너무 멀리 갔을 때
        if (Vector3.Distance(wrist.position, Vector3.zero) > 10f)
        {
            AddReward(-2f); // 경계 벗어남 페널티
            EndEpisode();
        }
    }

    // public override void Heuristic(in ActionBuffers actionsOut)
    // {
    //     // 수동 테스트용 입력
    //     var continuousActionsOut = actionsOut.ContinuousActions;
    //
    //     // 손목 이동 (WASD, QE)
    //     continuousActionsOut = Input.GetAxis("Horizontal"); // X
    //     continuousActionsOut = Input.GetKey(KeyCode.Q) ? 1f : Input.GetKey(KeyCode.E) ? -1f : 0f; // Y
    //     continuousActionsOut = Input.GetAxis("Vertical"); // Z
    //
    //     // 손목 회전 (화살표 키)
    //     continuousActionsOut = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
    //     continuousActionsOut = Input.GetKey(KeyCode.LeftArrow) ? -1f : Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
    //     continuousActionsOut = 0f;
    //
    //     // 손가락 제어 (숫자 키)
    //     for (int i = 6; i < continuousActionsOut.Length; i++)
    //     {
    //         continuousActionsOut[i] = Input.GetKey(KeyCode.Space) ? 1f : 0f; // 스페이스로 모든 손가락 구부리기
    //     }
    // }

    void OnTriggerEnter(Collider other)
    {
        if (other.transform == target)
        {
            AddReward(0.5f); // 물체에 닿았을 때 작은 보상
        }
    }
}