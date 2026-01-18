using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaucetController : MonoBehaviour // 或者直接修改 WaterTubeController
{
    [SerializeField] bool _update = true;
    [SerializeField] Transform _CreationPoint; // 赋值为水龙头的出水口位置
    [SerializeField] GameObject _WaterTubePrefab; // 赋值为 WaterTube.prefab

    private void Update()
    {
        if (!_update) return;

        // 示例：按下空格键模拟打开水龙头
        if (Input.GetKeyDown(KeyCode.B)) 
        {
            OpenFaucet();
        }
    }

    public void OpenFaucet()
    {
        // 关键修改：直接使用向下方向，而不是通过 hitPoint 计算
        Vector3 direction = Vector3.down; 
        
        GameObject waterTube = Instantiate(_WaterTubePrefab, _CreationPoint.position,_CreationPoint.rotation);

    }
}