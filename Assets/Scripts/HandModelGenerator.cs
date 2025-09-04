using UnityEngine;
using UnityEditor;

public class HandModelGenerator : EditorWindow
{
    [MenuItem("ML-Agents/Generate Hand Model")]
    static void ShowWindow()
    {
        GetWindow<HandModelGenerator>("Hand Model Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("손 모델 자동 생성", EditorStyles.boldLabel);

        if (GUILayout.Button("기본 손 모델 생성"))
        {
            GenerateHandModel();
        }

        if (GUILayout.Button("훈련 영역 생성"))
        {
            GenerateTrainingArea();
        }
    }

    static void GenerateHandModel()
    {
        GameObject handAgent = new GameObject("HandAgent");
        handAgent.transform.position = new Vector3(0, 1, 0);

        // Wrist 생성
        GameObject wrist = new GameObject("Wrist");
        wrist.transform.parent = handAgent.transform;
        wrist.transform.localPosition = Vector3.zero;

        // HandTip 생성
        GameObject handTip = new GameObject("HandTip");
        handTip.transform.parent = wrist.transform;
        handTip.transform.localPosition = new Vector3(0, 0, 0.5f);

        // 손가락 생성
        CreateFinger(wrist.transform, "Thumb", 4, new Vector3(-0.2f, 0, 0), new Vector3(0, 0, 30));
        CreateFinger(wrist.transform, "Index", 4, new Vector3(-0.15f, 0, 0.4f), Vector3.zero);
        CreateFinger(wrist.transform, "Middle", 3, new Vector3(0, 0, 0.45f), Vector3.zero);
        CreateFinger(wrist.transform, "Ring", 3, new Vector3(0.15f, 0, 0.4f), Vector3.zero);

        // HandAgent 스크립트 추가
        handAgent.AddComponent<HandAgent>();

        // ML-Agents 컴포넌트 추가
        handAgent.AddComponent<Unity.MLAgents.DecisionRequester>();
        var behaviorParams = handAgent.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        behaviorParams.BehaviorName = "HandAgent";
        behaviorParams.BrainParameters.VectorObservationSize = 71;
    
        behaviorParams.BrainParameters.ActionSpec = new Unity.MLAgents.Actuators.ActionSpec
        {
            NumContinuousActions = 20,  // Continuous 액션 크기 (NumActions 대체)
            BranchSizes = new int[0]    // Discrete 없음 (SpaceType.Continuous 대체) - 빈 배열로 수정
        };

        Debug.Log("손 모델이 생성되었습니다!");
        Selection.activeGameObject = handAgent;
    }


    static void CreateFinger(Transform parent, string fingerName, int jointCount, Vector3 startPos, Vector3 rotation)
    {
        GameObject finger = new GameObject(fingerName);
        finger.transform.parent = parent;
        finger.transform.localPosition = startPos;
        finger.transform.localRotation = Quaternion.Euler(rotation);

        Transform currentParent = finger.transform;
        Vector3 jointOffset = Vector3.forward * 0.25f;

        for (int i = 1; i <= jointCount; i++)
        {
            GameObject joint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            joint.name = $"{fingerName}_Joint{i}";
            joint.transform.parent = currentParent;
            joint.transform.localPosition = jointOffset;

            // 관절 크기 설정
            float scale = 0.1f - (i * 0.01f);
            joint.transform.localScale = new Vector3(scale, scale * 0.5f, scale * 2f);

            // ArticulationBody 추가
            var articulationBody = joint.AddComponent<ArticulationBody>();
            articulationBody.anchorRotation = Quaternion.Euler(90, 0, 0);

            var drive = articulationBody.xDrive;
            drive.stiffness = 10000;
            drive.damping = 100;
            drive.lowerLimit = -45;
            drive.upperLimit = 45;
            articulationBody.xDrive = drive;

            currentParent = joint.transform;
            jointOffset = Vector3.forward * 0.2f;
        }
    }

    static void GenerateTrainingArea()
    {
        GameObject trainingArea = new GameObject("TrainingArea");
        trainingArea.transform.position = Vector3.zero;

        // Ground 생성
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.parent = trainingArea.transform;
        ground.transform.localScale = new Vector3(2, 1, 2);

        // Target 생성
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = "Target";
        target.transform.parent = trainingArea.transform;
        target.transform.position = new Vector3(2, 1.5f, 2);
        target.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        target.AddComponent<Rigidbody>().mass = 0.5f;

        Debug.Log("훈련 영역이 생성되었습니다!");
        Selection.activeGameObject = trainingArea;
    }
}
