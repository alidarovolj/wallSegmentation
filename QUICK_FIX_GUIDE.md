# 🚀 Quick Fix Guide - 30 Seconds

## Your console shows these errors:
- ❌ `No active XROcclusionSubsystem available`
- ❌ `Не найдена подходящая SegFormer модель!`  
- ❌ `Материал для финиша Matte не найден`

## ✅ Instant Fix (30 seconds):

### Step 1: Add the Fix Component
1. **Right-click in Hierarchy** → Create Empty
2. **Name it:** "System Fixer"
3. **In Inspector:** Add Component → **SimpleSystemFix**

### Step 2: Run the Fix
1. **Press Play** ▶️
2. **Wait 5 seconds** for console messages
3. **Look for:** `🎉 All fixes completed!`

### Step 3: Verify Fix
**Console should now show:**
```
✅ SegFormer model assigned: segformer-b0-ade-512x512
✅ All paint materials created successfully  
📱 XR Configuration Guidance provided
🎉 Simple System Fix completed successfully!
```

---

## 🎯 What the fix does:

### ✅ Fixes SegFormer Model Issue
- Finds the best SegFormer model in your project
- Assigns it to AsyncSegmentationManager
- Replaces problematic SAM model

### ✅ Creates Missing Materials
- Creates Matte paint material
- Creates Satin, Gloss, SemiGloss materials
- Saves them to Assets/Materials/

### ✅ Provides XR Guidance
- Shows how to enable ARCore/ARKit
- Explains XR configuration steps

---

## 🔍 If you want to diagnose first:

### Option: Run Diagnostic
1. **Create empty GameObject:** "Diagnostic"
2. **Add Component:** QuickDiagnostic  
3. **Press Play** to see detailed analysis

---

## 🎉 After the fix:

Your system should work without errors. The console will show:
```
✅ AsyncSegmentationManager: ✅ Найден
✅ ARWallPresenter: ✅ Найден  
🎨 PaintRenderer инициализирован
🎉 Dulux Visualizer готов к работе!
```

**Total time: 30 seconds** ⏱️