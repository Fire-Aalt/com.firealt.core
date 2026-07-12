using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FireAlt.Core.Rendering
{
    /// <summary>
    /// Creates an instance of a material of a VisualElement
    /// </summary>
    public class VisualElementMaterial
    {
        private readonly VisualElement _target;
        private Material _instance;
        private Material _source;
        private IVisualElementScheduledItem _scheduledCreation;
        private int _scheduleVersion;
        
        public Material Instance => _instance;
        public bool IsCreated => _instance != null;
        public event Action<Material> Created;
        
        public VisualElementMaterial(VisualElement target)
        {
            _target = target;
            _target.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _target.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            if (_target.panel != null)
            {
                ScheduleCreate();
            }
        }

        private bool TryCreate()
        {
            if (_instance != null)
            {
                return true;
            }

            if (!Application.isPlaying)
            {
                return false;
            }

            _source = _target.resolvedStyle.unityMaterial.material;
            if (_source == null)
            {
                return false;
            }

            _instance = new Material(_source);
            _target.style.unityMaterial = _instance;
            Created?.Invoke(_instance);
            return true;
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ScheduleCreate();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            _scheduleVersion++;
            _scheduledCreation?.Pause();
            _scheduledCreation = null;

            if (_instance == null)
            {
                return;
            }

            _target.style.unityMaterial = _source;
            Object.Destroy(_instance);
            _instance = null;
            _source = null;
        }

        private void ScheduleCreate()
        {
            if (!Application.isPlaying || _instance != null)
            {
                return;
            }

            var version = ++_scheduleVersion;
            ScheduleCreate(version, 2);
        }

        private void ScheduleCreate(int version, int framesRemaining)
        {
            _scheduledCreation?.Pause();
            _scheduledCreation = _target.schedule.Execute(() =>
            {
                if (version != _scheduleVersion || _target.panel == null)
                {
                    return;
                }

                if (framesRemaining > 1)
                {
                    ScheduleCreate(version, framesRemaining - 1);
                    return;
                }

                if (!TryCreate())
                {
                    ScheduleCreate(version, 1);
                }
                else
                {
                    _scheduledCreation = null;
                }
            });
        }
    }
}
