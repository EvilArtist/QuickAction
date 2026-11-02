#include <windows.h>
#include <dwmapi.h>
#include <DispatcherQueue.h>
#include <windows.ui.composition.interop.h>
#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>
#include <memory>

using namespace winrt;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Composition::Desktop;

struct Backdrop
{
    HWND content = nullptr;
    HWND window = nullptr;
    HWND previousOwner = nullptr;
    bool syncing = false;
    Windows::System::DispatcherQueueController queue{ nullptr };
    Compositor compositor{ nullptr };
    DesktopWindowTarget target{ nullptr };
    ContainerVisual root{ nullptr };
    SpriteVisual blur{ nullptr };
    SpriteVisual tint{ nullptr };
    CompositionColorBrush tintBrush{ nullptr };
    CompositionRoundedRectangleGeometry geometry{ nullptr };
    ~Backdrop()
    {
        if (window)
        {
            syncing = true;
            if (IsWindow(content)) SetWindowLongPtr(content, GWLP_HWNDPARENT, reinterpret_cast<LONG_PTR>(previousOwner));
            try { if (target) target.Root(nullptr); } catch (...) { }
            DestroyWindow(window);
        }
    }
};

static LRESULT CALLBACK BackdropWindowProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam)
{
    if (message == WM_NCHITTEST) return HTTRANSPARENT;
    if (message == WM_MOUSEACTIVATE) return MA_NOACTIVATE;
    return DefWindowProcW(hwnd, message, wparam, lparam);
}

extern "C" __declspec(dllexport) void __cdecl SyncBackdrop(Backdrop* state) noexcept
{
    if (state->syncing) return;
    state->syncing = true;
    if (!IsWindowVisible(state->content) || IsIconic(state->content))
    {
        ShowWindow(state->window, SW_HIDE);
        state->syncing = false;
        return;
    }
    RECT bounds{};
    if (!GetWindowRect(state->content, &bounds)) { state->syncing = false; return; }
    // The WPF window is owned by the background, so Windows keeps its pixels
    // and input above the composition surface, including activation/topmost changes.
    SetWindowPos(state->window, state->content, bounds.left, bounds.top,
        bounds.right - bounds.left, bounds.bottom - bounds.top, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    state->syncing = false;
}

extern "C" __declspec(dllexport) HRESULT __cdecl CreateBackdrop(HWND hwnd, Backdrop** result) noexcept
{
    *result = nullptr;
    try
    {
        auto state = std::make_unique<Backdrop>();
        state->content = hwnd;
        state->previousOwner = reinterpret_cast<HWND>(GetWindowLongPtr(hwnd, GWLP_HWNDPARENT));
        WNDCLASSW windowClass{};
        windowClass.lpfnWndProc = BackdropWindowProc;
        windowClass.hInstance = GetModuleHandle(nullptr);
        windowClass.lpszClassName = L"QuickAction.CompositionBackdrop";
        if (!RegisterClassW(&windowClass) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS) throw_last_error();
        state->window = CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
            windowClass.lpszClassName, L"QuickAction background", WS_POPUP, 0, 0, 1, 1,
            nullptr, nullptr, windowClass.hInstance, nullptr);
        if (!state->window) throw_last_error();
        SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, reinterpret_cast<LONG_PTR>(state->window));
        if (!Windows::System::DispatcherQueue::GetForCurrentThread())
        {
            DispatcherQueueOptions options{ sizeof(options), DQTYPE_THREAD_CURRENT, DQTAT_COM_STA };
            check_hresult(CreateDispatcherQueueController(options,
                reinterpret_cast<ABI::Windows::System::IDispatcherQueueController**>(put_abi(state->queue))));
        }
        state->compositor = Compositor();
        auto interop = state->compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
        check_hresult(interop->CreateDesktopWindowTarget(state->window, false,
            reinterpret_cast<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget**>(put_abi(state->target))));
        BOOL enabled = TRUE;
        check_hresult(DwmSetWindowAttribute(state->window, DWMWA_USE_HOSTBACKDROPBRUSH, &enabled, sizeof(enabled)));
        state->root = state->compositor.CreateContainerVisual();
        state->geometry = state->compositor.CreateRoundedRectangleGeometry();
        state->root.Clip(state->compositor.CreateGeometricClip(state->geometry));
        state->blur = state->compositor.CreateSpriteVisual();
        state->blur.Brush(state->compositor.CreateHostBackdropBrush());
        state->tint = state->compositor.CreateSpriteVisual();
        state->tintBrush = state->compositor.CreateColorBrush({ 209,248,250,255 });
        state->tint.Brush(state->tintBrush);
        state->root.Children().InsertAtTop(state->blur);
        state->root.Children().InsertAtTop(state->tint);
        state->target.Root(state->root);
        *result = state.release();
        return S_OK;
    }
    catch (...) { return to_hresult(); }
}

extern "C" __declspec(dllexport) HRESULT __cdecl UpdateBackdrop(Backdrop* state,
    float x,float y,float width,float height,float radius,float opacity,BOOL enabled) noexcept
{
    try
    {
        state->root.Offset({ x,y,0 });
        state->root.Size({ width,height });
        state->geometry.Size({ width,height });
        state->geometry.CornerRadius({ radius,radius });
        state->blur.Size({ width,height });
        state->root.IsVisible(enabled != FALSE);
        state->tint.Size({ width,height });
        state->tintBrush.Color({ static_cast<uint8_t>(opacity*255),248,250,255 });
        return S_OK;
    }
    catch (...) { return to_hresult(); }
}

extern "C" __declspec(dllexport) void __cdecl DestroyBackdrop(Backdrop* state) noexcept
{
    delete state;
}
