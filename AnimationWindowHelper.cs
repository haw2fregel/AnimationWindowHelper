using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

/// <summary>
/// イージングのプリセット化するための補助的クラス
/// Animationウィンドウへのアクセスを提供する。
/// </summary>
public class AnimationWindowHelper
{
    //AnimationWindow関連のTypeを取得しておく
    private static Type animationWindowType = Type.GetType("UnityEditor.AnimationWindow, UnityEditor");
    private static Type animationWindowStateType = Type.GetType("UnityEditorInternal.AnimationWindowState, UnityEditor");
    private static Type animationWindowKeyframeType = Type.GetType("UnityEditorInternal.AnimationWindowKeyframe, UnityEditor");
    private static Type animationWindowCurveType = Type.GetType("UnityEditorInternal.AnimationWindowCurve, UnityEditor");
    private static Type animEditorType = Type.GetType("UnityEditor.AnimEditor, UnityEditor");

    /// <summary>
    /// Animationウィンドウで選択中のキーフレームが持つイージングをAnimationCurveで取得する。
    /// time,valueを0~1に補正
    /// </summary>
    /// <returns></returns>
    public static AnimationCurve GetSelectionEasing()
    {
        //Animationウィンドウを取得する。開いてなけれ開き、そのインスタンスを取得する。
        var animationWindow = EditorWindow.GetWindow(animationWindowType,false,null,true);

        //AnimationWindowstateを取得するため、AnimEditorを取得
        var animEditorProperty = animationWindowType.GetProperty("animEditor", BindingFlags.Instance | BindingFlags.NonPublic);
        var animEditor = animEditorProperty.GetValue(animationWindow);

        //Animationウィンドウ内の情報取得のため、AnimationWindowstateを取得。
        var stateProperty = animEditorType.GetProperty("state", BindingFlags.Instance | BindingFlags.Public);
        var state = stateProperty.GetValue(animEditor);

        //今開いてるのがCurveWindowの場合は選択中のキーフレームが上手く取得できないので、
        //一度DopeSheetに戻してSelectionKeyの更新をする
        var showCurveEditor = animationWindowStateType.GetField("showCurveEditor", BindingFlags.Instance | BindingFlags.Public);
        if((bool)showCurveEditor.GetValue(state))
        {
            var onEnableWindow = animEditorType.GetMethod("SwitchBetweenCurvesAndDopesheet", BindingFlags . Instance | BindingFlags . NonPublic | BindingFlags . Public);
            onEnableWindow.Invoke(animEditor,null); // DopeSheetに切り替えたことでSelectionKeyが更新される
            onEnableWindow.Invoke(animEditor,null); // 更新が終わったのでCurveWindowに戻す
        }

        //選択中のキーフレーム取得のため、AnimationWindowState.selectedKeysを取得
        //AnimationWindowKeyframe型のListで返ってくるが、そのままだと扱えないのでdynamic型で受け取り
        var getSelectionProperty = animationWindowStateType.GetProperty("selectedKeys", BindingFlags.Instance | BindingFlags.Public);
        var keys = getSelectionProperty.GetValue(state) as dynamic;

        //keys[0]だけ扱えれば良いが、dynamic型だとindex参照できないため、foreachで無理やり参照する
        //1週だけでいいので最後にreturn
        foreach (var key in keys)
        {
            //選択しているキーフレームを含む、AnimationWindowCurveを取得
            var curveProperty = animationWindowKeyframeType.GetProperty("curve", BindingFlags.Instance | BindingFlags.Public);
            var selectWindowCurve = curveProperty.GetValue(key);

            //取得したAnimationWindowCurveをUnityEngene.AnimationCurveに変換
            var toAnimationCurve = animationWindowCurveType.GetMethod("ToAnimationCurve" ,  BindingFlags.Instance | BindingFlags.Public);
            var animationCurve = toAnimationCurve.Invoke(obj:selectWindowCurve , parameters: null) as AnimationCurve;
            
            //選択したキーフレームがAnimationCurve内の何番目か取得
            var getIndex = animationWindowKeyframeType.GetMethod("GetIndex" ,  BindingFlags.Instance | BindingFlags.Public);
            var index = (int)getIndex.Invoke(key , null);

            //今回の場合選択キーと次のキーイージングを編集するので、最終キーが選択されている場合はNG
            if(animationCurve.length <= index + 1)
            {
                Debug.Log("最終キーフレームからは取得できません。");
                return null;
            }

            //扱いやすいようにtime,valueの幅を0~1に正規化したカーブに変換してから保存する。
            var fromKey = animationCurve.keys[index];
            var toKey = animationCurve.keys[index+1];

            var deltaTime = toKey.time - fromKey.time;
            var deltaValue = toKey.value - fromKey.value;
            
            var timeDivValue = deltaTime / deltaValue;
            var normalize = timeDivValue / 1;

            fromKey.outTangent = fromKey.outTangent * normalize;
            toKey.inTangent = toKey.inTangent * normalize;

            fromKey.time = 0;
            fromKey.value = 0;
            
            toKey.time = 1;
            toKey.value = 1;

            return new AnimationCurve(fromKey, toKey);

        }
        //キーが選択されていない場合、foreachに入らないので警告出して終了
        Debug.Log("キーフレームが選択されていません。");
        return null;
    }

    /// <summary>
    /// AnimationCurveを渡すと、Animationウィンドウで選択中のキーフレームにイージングを反映させる。
    /// </summary>
    /// <param name="curve"></param>
    public static void SetEasing(AnimationCurve curve)
    {
        //Animationウィンドウを取得する。開いてなけれ開き、そのインスタンスを取得する。
        var animationWindow = EditorWindow.GetWindow(animationWindowType,false,null,true);

        //AnimationWindowstateを取得するため、AnimEditorを取得
        var animEditorProperty = animationWindowType.GetProperty("animEditor", BindingFlags.Instance | BindingFlags.NonPublic);
        var animEditor = animEditorProperty.GetValue(animationWindow);

        //Animationウィンドウ内の情報取得のため、AnimationWindowstateを取得。
        var stateProperty = animEditorType.GetProperty("state", BindingFlags.Instance | BindingFlags.Public);
        var state = stateProperty.GetValue(animEditor);

        //今開いてるのがCurveWindowの場合は選択中のキーフレームが上手く取得できないので、
        //一度DopeSheetに戻してSelectionKeyの更新をする
        var showCurveEditor = animationWindowStateType.GetField("showCurveEditor", BindingFlags.Instance | BindingFlags.Public);
        if((bool)showCurveEditor.GetValue(state))
        {
            var onEnableWindow = animEditorType.GetMethod("SwitchBetweenCurvesAndDopesheet", BindingFlags . Instance | BindingFlags . NonPublic | BindingFlags . Public);
            onEnableWindow.Invoke(animEditor,null); // DopeSheetに切り替えたことでSelectionKeyが更新される
            onEnableWindow.Invoke(animEditor,null); // 更新が終わったのでCurveWindowに戻す
        }

        //選択中のキーフレーム取得のため、AnimationWindowState.selectedKeysを取得
        //AnimationWindowKeyframe型のListで返ってくるが、そのままだと扱えないのでdynamic型で受け取り
        var getSelectionProperty = animationWindowStateType.GetProperty("selectedKeys", BindingFlags.Instance | BindingFlags.Public);
        var keys = getSelectionProperty.GetValue(state) as dynamic;

        //キーが選択されているか判定用
        var isKeySelection = false;

        //選択されている全てのキーにイージングを反映する。
        foreach (var key in keys)
        {
            //foreach内に入ったのでキー選択されている
            isKeySelection = true;

            //選択しているキーフレームを含む、AnimationWindowCurveを取得
            var curveProperty = animationWindowKeyframeType.GetProperty("curve", BindingFlags.Instance | BindingFlags.Public);
            var selectWindowCurve = curveProperty.GetValue(key);

            //AnimationWindowCurveのままだと編集しにくいので、UnityEngene.AnimationCurveに変換する
            var toAnimationCurve = animationWindowCurveType.GetMethod("ToAnimationCurve" ,  BindingFlags.Instance | BindingFlags.Public);
            var animationCurve = toAnimationCurve.Invoke(obj:selectWindowCurve , parameters: null) as AnimationCurve;

            //選択したキーフレームがAnimationCurve内の何番目か取得
            var getIndex = animationWindowKeyframeType.GetMethod("GetIndex" ,  BindingFlags.Instance | BindingFlags.Public);
            var index = (int)getIndex.Invoke(key , null);

            //今回の場合選択キーと次のキーイージングを編集するので、最終キーが選択されている場合はNG
            if(animationCurve.length <= index + 1)
            {
                Debug.Log("最終キーフレームには設定できません。");
                return;
            }

            var fromSelectKey = animationCurve.keys[index];//選択したキーフレーム情報
            var toSelectKey = animationCurve.keys[index + 1];//次のキーフレーム情報

            //書き換えるキーフレームのValueに合わせてTangentを変換
            var fromKey = curve.keys[0];
            var toKey = curve.keys[1];

            var deltaTime = toKey.time - fromKey.time;
            var deltaTime1 = toSelectKey.time - fromSelectKey.time;

            var deltaValue = toKey.value - fromKey.value;
            var deltaValue1 = toSelectKey.value - fromSelectKey.value;

            var timeDivValue = deltaTime / deltaValue;
            var timeDivValue1 = deltaTime1 / deltaValue1;

            var beforeDivValue = timeDivValue / timeDivValue1;

            fromSelectKey.outTangent = fromKey.outTangent * beforeDivValue;
            toSelectKey.inTangent = toKey.inTangent * beforeDivValue;
            fromSelectKey.outWeight = fromKey.outWeight;
            toSelectKey.inWeight = toKey.inWeight;

            //WeightModeを更新
            fromSelectKey.weightedMode =  SetOutWeightMode(fromSelectKey.weightedMode, fromKey.weightedMode);
            toSelectKey.weightedMode = SetInWeightMode(toSelectKey.weightedMode, toKey.weightedMode);

            //キーフレームの編集をAnimationCurveに通知
            animationCurve.MoveKey(index, fromSelectKey);
            animationCurve.MoveKey(index + 1, toSelectKey);

            //保存先のAnimationClipを取得
            var clipProperty = animationWindowCurveType.GetProperty("clip", BindingFlags.Instance | BindingFlags.Public);
            var clip = clipProperty.GetValue(selectWindowCurve);
            //AnimationCurve判別のためにCurveBindingを取得
            var bindingProperty = animationWindowCurveType.GetProperty("binding", BindingFlags.Instance | BindingFlags.Public);
            var binding = bindingProperty.GetValue(selectWindowCurve);

            //AnimationClipにAnimationCurveの編集を反映
            AnimationUtility.SetEditorCurve(clip, binding, animationCurve);

        }

        //キーフレームを選択してない場合foreachの中に入らず警告表示
        if(!isKeySelection)
        {
            Debug.Log("キーフレームが選択されていません。");
        }

        return;

    }

    /// <summary>
    /// WeightModeを更新する。
    /// 手前のキー
    /// </summary>
    /// <param name="beforeMode"></param>
    /// <param name="overrideMode"></param>
    /// <returns></returns>
    static WeightedMode SetOutWeightMode(WeightedMode beforeMode, WeightedMode overrideMode)
    {
        if(beforeMode == overrideMode) return overrideMode;
        if(overrideMode == WeightedMode.Out || overrideMode == WeightedMode.Both)
        {
            if(beforeMode == WeightedMode.None)
            {
                return WeightedMode.Out;
            }
            if(beforeMode == WeightedMode.In)
            {
                return WeightedMode.Both;
            }
        }else
        {
            if(beforeMode == WeightedMode.Out)
            {
                return WeightedMode.None;
            }
            if(beforeMode == WeightedMode.Both)
            {
                return  WeightedMode.In;
            }
        }
        return WeightedMode.None;
    }

    /// <summary>
    /// WeightModeを更新する。
    /// 奥のキー
    /// </summary>
    /// <param name="beforeMode"></param>
    /// <param name="overrideMode"></param>
    /// <returns></returns>
    static WeightedMode SetInWeightMode(WeightedMode beforeMode, WeightedMode overrideMode)
    {
        if(beforeMode == overrideMode) return overrideMode;
        if(overrideMode == WeightedMode.In || overrideMode == WeightedMode.Both)
        {
            if(beforeMode == WeightedMode.None)
            {
                return WeightedMode.In;
            }
            if(beforeMode == WeightedMode.Out)
            {
                return WeightedMode.Both;
            }
        }else
        {
            if(beforeMode == WeightedMode.In)
            {
                return WeightedMode.None;
            }
            if(beforeMode == WeightedMode.Both)
            {
                return  WeightedMode.Out;
            }
        }
        return WeightedMode.None;
    }

}