﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;
using Contracts;
using ContractConfigurator;

namespace ContractConfigurator.Behaviour
{
    /// <summary>
    /// Behaviour for displaying a rich text dialog box.
    /// </summary>
    public class DialogBox : ContractBehaviour
    {
        #region enums
        public enum TriggerCondition
        {
            CONTRACT_FAILED,
            CONTRACT_ACCEPTED,
            CONTRACT_COMPLETED,
            PARAMETER_FAILED,
            PARAMETER_COMPLETED
        }

        public enum Position
        {
            LEFT,
            CENTER,
            RIGHT
        }
        #endregion

        #region DialogBoxGUI
        public class DialogBoxGUI : MonoBehaviour
        {
            public static GUIStyle windowStyle;

            private Rect windowPos = new Rect(0, 0, 0, 0);
            private bool visible = false;
            private DialogBox dialogBox;

            void Start()
            {
                DontDestroyOnLoad(this);
            }

            void OnGUI()
            {
                if (visible && MissionControl.Instance == null)
                {
                    DialogBox.DialogDetail detail = dialogBox.displayQueue.FirstOrDefault();
                    if (detail == null)
                    {
                        visible = false;
                        Destroy(this);
                        return;
                    }

                    if (windowPos.width == 0 && windowPos.height == 0)
                    {
                        float multiplier = (4.0f / 3.0f) / ((float)Screen.width / Screen.height);

                        float w = Screen.width * detail.width - 32;
                        float h = Screen.height * detail.height - 144f;
                        float x = detail.position == Position.LEFT ? 16f : detail.position == Position.CENTER ? (Screen.width - w) / 2.0f : (Screen.width - w - 16f);
                        windowPos = new Rect(x, 72f, w, h);
                    }

                    UnityEngine.GUI.skin = HighLogic.Skin;
                    windowPos = GUILayout.Window(GetType().FullName.GetHashCode(),
                        windowPos, DrawMessageBox, detail.title, windowStyle ?? HighLogic.Skin.window, GUILayout.Width(Screen.width * detail.width));
                }
            }

            void DrawMessageBox(int windowID)
            {
                DialogBox.DialogDetail detail = dialogBox.displayQueue.FirstOrDefault();

                if (windowStyle == null)
                {
                    windowStyle = new GUIStyle(HighLogic.Skin.window);
                    windowStyle.alignment = TextAnchor.UpperLeft;
                    windowStyle.active.textColor = detail.titleColor;
                    windowStyle.focused.textColor = detail.titleColor;
                    windowStyle.hover.textColor = detail.titleColor;
                    windowStyle.normal.textColor = detail.titleColor;
                    windowStyle.onActive.textColor = detail.titleColor;
                    windowStyle.onFocused.textColor = detail.titleColor;
                    windowStyle.onHover.textColor = detail.titleColor;
                    windowStyle.onNormal.textColor = detail.titleColor;
                }

                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Space(8);
                foreach (Section section in detail.sections)
                {
                    section.OnGUI();
                    GUILayout.Space(8);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK", GUILayout.MinWidth(80)))
                {
                    windowPos = new Rect(0, 0, 0, 0);
                    windowStyle = null;
                    foreach (Section section in detail.sections)
                    {
                        section.OnDestroy();
                    }
                    dialogBox.displayQueue.Dequeue();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                UnityEngine.GUI.DragWindow();
            }

            public void OnPreCull()
            {
                DialogBox.DialogDetail detail = dialogBox.displayQueue.FirstOrDefault();
                if (detail == null)
                {
                    return;
                }
                foreach (Section section in detail.sections)
                {
                    section.OnPreCull();
                }
            }

            public static void DisplayMessage(DialogBox dialogBox)
            {
                DialogBoxGUI gui = MapView.MapCamera.gameObject.GetComponent<DialogBoxGUI>();
                if (gui == null)
                {
                    LoggingUtil.LogVerbose(typeof(DialogBox), "Adding DialogBoxGUI");
                    gui = MapView.MapCamera.gameObject.AddComponent<DialogBoxGUI>();
                }

                gui.Show(dialogBox);
            }

            private void Show(DialogBox dialogBox)
            {
                visible = true;
                this.dialogBox = dialogBox;
            }
        }
        #endregion

        #region Section
        public abstract class Section
        {
            public abstract void OnGUI();
            public abstract void OnSave(ConfigNode configNode);
            public abstract void OnLoad(ConfigNode configNode);
            public virtual void OnDestroy() { }
            public virtual void OnPreCull() { }
        }

        public class TextSection : Section
        {
            public string text;
            public Color textColor;
            public int fontSize;
            public GUIStyle labelStyle;

            public TextSection()
            {
            }

            public override void OnGUI()
            {
                if (labelStyle == null)
                {
                    labelStyle = new GUIStyle(UnityEngine.GUI.skin.label);
                    labelStyle.alignment = TextAnchor.UpperLeft;
                    labelStyle.richText = true;
                    labelStyle.normal.textColor = textColor;
                    labelStyle.fontSize = fontSize;
                }

                GUILayout.Label(text, labelStyle, GUILayout.ExpandWidth(true));
            }

            public override void OnSave(ConfigNode configNode)
            {
                configNode.AddValue("text", text.Replace("\n", "\\n"));
                int a = (int)(textColor.a * 255);
                int r = (int)(textColor.r * 255);
                int g = (int)(textColor.g * 255);
                int b = (int)(textColor.b * 255);
                configNode.AddValue("textColor", "#" + a.ToString("X2") + r.ToString("X2") + g.ToString("X2") + b.ToString("X2"));
                configNode.AddValue("fontSize", fontSize);
            }

            public override void OnLoad(ConfigNode configNode)
            {
                text = ConfigNodeUtil.ParseValue<string>(configNode, "text");
                textColor = ConfigNodeUtil.ParseValue<Color>(configNode, "textColor");
                fontSize = ConfigNodeUtil.ParseValue<int>(configNode, "fontSize");
            }
        }

        public abstract class NamedSection : Section
        {
            public bool showName;
            public string characterName;
            public Color textColor;

            private GUIStyle labelStyle;

            public NamedSection()
            {
            }

            protected void DisplayName(float width)
            {
                if (labelStyle == null)
                {
                    labelStyle = new GUIStyle(UnityEngine.GUI.skin.label);
                    labelStyle.alignment = TextAnchor.UpperCenter;
                    labelStyle.normal.textColor = textColor;
                    labelStyle.fontStyle = FontStyle.Bold;
                }

                if (showName)
                {
                    GUILayout.Label(characterName, labelStyle, GUILayout.Width(width));
                }
            }

            public override void OnSave(ConfigNode configNode)
            {
                configNode.AddValue("showName", showName);
                if (!string.IsNullOrEmpty(characterName))
                {
                    configNode.AddValue("characterName", characterName);
                }
                int a = (int)(textColor.a * 255);
                int r = (int)(textColor.r * 255);
                int g = (int)(textColor.g * 255);
                int b = (int)(textColor.b * 255);
                configNode.AddValue("textColor", "#" + a.ToString("X2") + r.ToString("X2") + g.ToString("X2") + b.ToString("X2"));
            }

            public override void OnLoad(ConfigNode configNode)
            {
                showName = ConfigNodeUtil.ParseValue<bool>(configNode, "showName");
                characterName = ConfigNodeUtil.ParseValue<string>(configNode, "characterName", "");
                textColor = ConfigNodeUtil.ParseValue<Color>(configNode, "textColor");
            }
        }


        public class ImageSection : NamedSection
        {
            public string imageURL;
            private Texture2D image = null;

            public ImageSection()
            {
            }

            public override void OnGUI()
            {
                if (image == null)
                {
                    image = GameDatabase.Instance.GetTexture(imageURL, false);
                }

                GUILayout.BeginVertical(GUILayout.Width(image.width));
                GUILayout.Label(image);
                DisplayName(image.width);
                GUILayout.EndVertical();
            }

            public override void OnSave(ConfigNode configNode)
            {
                base.OnSave(configNode);
                configNode.AddValue("imageURL", imageURL);
            }

            public override void OnLoad(ConfigNode configNode)
            {
                base.OnLoad(configNode);
                imageURL = ConfigNodeUtil.ParseValue<string>(configNode, "imageURL");
            }
        }

        public class InstructorSection : NamedSection
        {
            public enum Animation
            {
                idle,
                idle_lookAround,
                idle_sigh,
                idle_wonder,
                true_thumbUp,
                true_thumbsUp,
                true_nodA,
                true_nodB,
                true_smileA,
                true_smileB,
                false_disappointed,
                false_disagreeA,
                false_disagreeB,
                false_disagreeC,
                false_sadA,
            }
            public string name;
            public Animation? animation;

            KerbalInstructor instructor = null;
            RenderTexture instructorTexture;
            GameObject lightGameObject = null;
            CharacterAnimationState animState = null;
            float nextAnimTime = float.MaxValue;

            static float offset = 0.0f;

            public InstructorSection()
            {
            }

            public override void OnDestroy()
            {
                if (instructor != null)
                {
                    UnityEngine.Object.Destroy(instructor.gameObject);
                }
                if (lightGameObject != null)
                {
                    UnityEngine.Object.Destroy(lightGameObject);
                }
            }

            public override void OnGUI()
            {
                if (instructor == null)
                {
                    instructor = ((GameObject)UnityEngine.Object.Instantiate(AssetBase.GetPrefab(name))).GetComponent<KerbalInstructor>();

                    instructorTexture = new RenderTexture(128, 128, 8);
                    instructor.instructorCamera.targetTexture = instructorTexture;
                    instructor.instructorCamera.ResetAspect();

                    offset += 25f;
                    instructor.gameObject.transform.Translate(offset, 0.0f, 0.0f);

                    if (name.StartsWith("Strategy"))
                    {
                        lightGameObject = new GameObject("Strategy Light");
                        Light lightComp = lightGameObject.AddComponent<Light>();
                        lightComp.color = new Color(0.4f, 0.4f, 0.4f);
                        lightGameObject.transform.position = instructor.instructorCamera.transform.position;
                    }

                    if (string.IsNullOrEmpty(characterName))
                    {
                        characterName = instructor.CharacterName;
                    }

                    instructor.SetupAnimations();

                    if (animation != null)
                    {
                        switch (animation.Value)
                        {
                            case Animation.idle:
                                animState = instructor.anim_idle;
                                break;
                            case Animation.idle_lookAround:
                                animState = instructor.anim_idle_lookAround;
                                break;
                            case Animation.idle_sigh:
                                animState = instructor.anim_idle_sigh;
                                break;
                            case Animation.idle_wonder:
                                animState = instructor.anim_idle_wonder;
                                break;
                            case Animation.true_thumbUp:
                                animState = instructor.anim_true_thumbUp;
                                break;
                            case Animation.true_thumbsUp:
                                animState = instructor.anim_true_thumbsUp;
                                break;
                            case Animation.true_nodA:
                                animState = instructor.anim_true_nodA;
                                break;
                            case Animation.true_nodB:
                                animState = instructor.anim_true_nodB;
                                break;
                            case Animation.true_smileA:
                                animState = instructor.anim_true_smileA;
                                break;
                            case Animation.true_smileB:
                                animState = instructor.anim_true_smileB;
                                break;
                            case Animation.false_disappointed:
                                animState = instructor.anim_false_disappointed;
                                break;
                            case Animation.false_disagreeA:
                                animState = instructor.anim_false_disagreeA;
                                break;
                            case Animation.false_disagreeB:
                                animState = instructor.anim_false_disagreeB;
                                break;
                            case Animation.false_disagreeC:
                                animState = instructor.anim_false_disagreeC;
                                break;
                            case Animation.false_sadA:
                                animState = instructor.anim_false_sadA;
                                break;
                        }

                        // Give a short delay before playing the animation
                        nextAnimTime = Time.fixedTime + 0.3f;
                    }
                }

                // Play the animation
                if (nextAnimTime <= Time.fixedTime)
                {
                    instructor.PlayEmote(animState);
                    animState.audioClip = null;
                    nextAnimTime = Time.fixedTime + animState.clip.length;
                }

                GUILayout.BeginVertical(GUILayout.Width(128));
                GUILayout.Box("", GUILayout.Width(128), GUILayout.Height(128));
                if (Event.current.type == EventType.Repaint)
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    rect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
                    Graphics.DrawTexture(rect, instructorTexture, new Rect(0.0f, 0.0f, 1f, 1f), 124, 124, 124, 124, Color.white, instructor.PortraitRenderMaterial);
                }

                DisplayName(128);

                GUILayout.EndVertical();
            }

            public override void OnSave(ConfigNode configNode)
            {
                base.OnSave(configNode);
                configNode.AddValue("name", name);
                if (animation != null)
                {
                    configNode.AddValue("animation", animation.Value);
                }
            }

            public override void OnLoad(ConfigNode configNode)
            {
                base.OnLoad(configNode);
                name = ConfigNodeUtil.ParseValue<string>(configNode, "name");
                animation = ConfigNodeUtil.ParseValue<Animation?>(configNode, "animation", (Animation?)null);
            }
        }

        public class KerbalSection : NamedSection
        {
            ProtoCrewMember kerbal = null;
            Camera kerbalCam = null;
            RenderTexture renderTexture;

            public KerbalSection()
            {
            }

            public override void OnDestroy()
            {
                if (kerbalCam != null && kerbalCam != kerbal.KerbalRef.kerbalCam)
                {
                    UnityEngine.Object.Destroy(kerbalCam);
                    UnityEngine.Object.Destroy(renderTexture);
                }
                kerbalCam = null;
                renderTexture = null;
            }

            public override void OnPreCull()
            {
                if (kerbal != null)
                {
                    if (kerbal.KerbalRef == null)
                    {
                        Vessel kerbEVA = FlightGlobals.Vessels.Where(v => v.isEVA && v.GetVesselCrew().Contains(kerbal)).FirstOrDefault();
                        if (kerbEVA)
                        {
                            if (kerbalCam == null)
                            {
                                renderTexture = new RenderTexture(128, 128, 8);

                                kerbalCam = (Camera)UnityEngine.Object.Instantiate(FlightCamera.fetch.mainCamera);
                                kerbalCam.targetTexture = renderTexture;
                                kerbalCam.clearFlags = CameraClearFlags.Color;
                                kerbalCam.backgroundColor = Color.black;
                                kerbalCam.clearStencilAfterLightingPass = true;
                                kerbalCam.depthTextureMode = DepthTextureMode.DepthNormals;
                                kerbalCam.useOcclusionCulling = false;
                                kerbalCam.cullingMask = (1 << 0) | (1 << 1) | (1 << 4) | (1 << 9) | (1 << 10) | (1 << 15) | (1 << 18) | (1 << 20) | (1 << 23);

                                kerbalCam.transform.parent = kerbEVA.transform;
                                kerbalCam.transform.localPosition = new Vector3();
                                kerbalCam.transform.Translate(new Vector3(0.0f, 0.75f, 0.33f));
                                kerbalCam.transform.LookAt(kerbEVA.transform.position + kerbEVA.transform.up * 0.33f, kerbEVA.transform.up);
                            }
                        }
                    }
                    else
                    {
                        renderTexture = kerbal.KerbalRef.avatarTexture;
                    }
                }
            }

            public override void OnGUI()
            {
                if (kerbal == null && FlightGlobals.ActiveVessel != null)
                {
                    kerbal = FlightGlobals.ActiveVessel.GetVesselCrew().FirstOrDefault();
                    
                    if (kerbal != null)
                    {
                        if (string.IsNullOrEmpty(characterName))
                        {
                            characterName = kerbal.name;
                        }
                    }
                }

                GUILayout.BeginVertical(GUILayout.Width(128));
                GUILayout.Box("", GUILayout.Width(128), GUILayout.Height(128));
                if (Event.current.type == EventType.Repaint && kerbal != null)
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    rect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
                    Graphics.DrawTexture(rect, renderTexture, new Rect(0.0f, 0.0f, 1f, 1f), 0, 0, 0, 0, Color.white, KerbalGUIManager.PortraitRenderMaterial);
                }

                DisplayName(128);

                GUILayout.EndVertical();
            }

            public override void OnSave(ConfigNode configNode)
            {
                base.OnSave(configNode);
            }

            public override void OnLoad(ConfigNode configNode)
            {
                base.OnLoad(configNode);
            }
        }

        public class BreakSection : Section
        {
            public BreakSection()
            {
            }

            public override void OnGUI()
            {
                GUILayout.Space(8);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
            }

            public override void OnSave(ConfigNode configNode)
            {
            }

            public override void OnLoad(ConfigNode configNode)
            {
            }
        }
        #endregion

        #region DialogDetail
        public class DialogDetail
        {
            public TriggerCondition condition;
            public Position position;
            public float width;
            public float height;
            public Color titleColor;
            public string title;
            public string parameter;
            public List<Section> sections = new List<Section>();

            public DialogDetail()
            {
            }

            public void OnSave(ConfigNode configNode)
            {
                configNode.AddValue("condition", condition);
                configNode.AddValue("position", position);
                configNode.AddValue("width", width);
                configNode.AddValue("height", height);
                if (!string.IsNullOrEmpty(title))
                {
                    configNode.AddValue("title", title);
                }
                if (!string.IsNullOrEmpty(parameter))
                {
                    configNode.AddValue("parameter", parameter);
                }

                int a = (int)(titleColor.a * 255);
                int r = (int)(titleColor.r * 255);
                int g = (int)(titleColor.g * 255);
                int b = (int)(titleColor.b * 255);
                configNode.AddValue("titleColor", "#" + a.ToString("X2") + r.ToString("X2") + g.ToString("X2") + b.ToString("X2"));

                foreach (Section section in sections)
                {
                    ConfigNode sectionNode = new ConfigNode(section.GetType().Name);
                    configNode.AddNode(sectionNode);
                    section.OnSave(sectionNode);
                }
            }

            public void OnLoad(ConfigNode configNode)
            {
                condition = ConfigNodeUtil.ParseValue<TriggerCondition>(configNode, "condition");
                position = ConfigNodeUtil.ParseValue<Position>(configNode, "position");
                width = ConfigNodeUtil.ParseValue<float>(configNode, "width");
                height = ConfigNodeUtil.ParseValue<float>(configNode, "height");
                title = ConfigNodeUtil.ParseValue<string>(configNode, "title", "");
                titleColor = ConfigNodeUtil.ParseValue<Color>(configNode, "titleColor");
                parameter = ConfigNodeUtil.ParseValue<string>(configNode, "parameter", "");

                IEnumerable<Type> sectionTypes = ContractConfigurator.GetAllTypes<Section>();
                foreach (ConfigNode sectionNode in configNode.GetNodes())
                {
                    Type type = sectionTypes.Where(t => t.Name == sectionNode.name).FirstOrDefault();
                    if (type == null)
                    {
                        throw new ArgumentException("Couldn't find dialog box section of type " + sectionNode.name);
                    }
                    Section section = (Section)Activator.CreateInstance(type);
                    section.OnLoad(sectionNode);
                    sections.Add(section);
                }
            }
        }
        #endregion

        private List<DialogDetail> details = new List<DialogDetail>();
        public Queue<DialogDetail> displayQueue = new Queue<DialogDetail>();

        public DialogBox()
            : base()
        {
        }

        public DialogBox(List<DialogDetail> details)
        {
            this.details = details;
        }

        protected override void OnParameterStateChange(ContractParameter param)
        {
            if (param.State == ParameterState.Incomplete)
            {
                return;
            }

            TriggerCondition cond = param.State == ParameterState.Complete ?
                TriggerCondition.PARAMETER_COMPLETED :
                TriggerCondition.PARAMETER_FAILED;

            foreach (DialogDetail detail in details.Where(d => d.condition == cond && d.parameter == param.ID))
            {
                displayQueue.Enqueue(detail);
            }

            if (displayQueue.Any())
            {
                DialogBoxGUI.DisplayMessage(this);
            }
        }

        protected override void OnAccepted()
        {
            foreach (DialogDetail detail in details.Where(d => d.condition == TriggerCondition.CONTRACT_ACCEPTED))
            {
                displayQueue.Enqueue(detail);
            }

            if (displayQueue.Any())
            {
                DialogBoxGUI.DisplayMessage(this);
            }
        }

        protected override void OnCompleted()
        {
            foreach (DialogDetail detail in details.Where(d => d.condition == TriggerCondition.CONTRACT_COMPLETED))
            {
                displayQueue.Enqueue(detail);
            }

            if (displayQueue.Any())
            {
                DialogBoxGUI.DisplayMessage(this);
            }
        }

        protected override void OnFailed()
        {
            foreach (DialogDetail detail in details.Where(d => d.condition == TriggerCondition.CONTRACT_FAILED))
            {
                displayQueue.Enqueue(detail);
            }

            if (displayQueue.Any())
            {
                DialogBoxGUI.DisplayMessage(this);
            }
        }

        protected override void OnSave(ConfigNode configNode)
        {
            foreach (DialogDetail detail in details)
            {
                ConfigNode child = new ConfigNode("DIALOG_BOX");
                configNode.AddNode(child);

                detail.OnSave(child);
            }
        }

        protected override void OnLoad(ConfigNode configNode)
        {
            foreach (ConfigNode child in configNode.GetNodes("DIALOG_BOX"))
            {
                DialogDetail detail = new DialogDetail();
                detail.OnLoad(child);

                details.Add(detail);
            }
        }
    }
}
