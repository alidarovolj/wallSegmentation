# 🚨 Immediate Fixes for Unity Console Errors

## Current Issues Identified:

### 1. ❌ XR Occlusion Subsystem Error
**Error:** `No active UnityEngine.XR.ARSubsystems.XROcclusionSubsystem is available`

### 2. ❌ Missing SegFormer Model 
**Error:** `❌ Не найдена подходящая SegFormer модель!`

### 3. ❌ Missing Matte Material
**Error:** `⚠️ Материал для финиша Matte не найден`

---

## ✅ Quick Fixes (5 minutes):

### Fix 1: Configure XR Settings
1. **Open Project Settings** (Edit → Project Settings)
2. **Go to XR Plug-in Management**
3. **Enable ARCore** (Android) or **ARKit** (iOS)
4. **Go to ARFoundation** section
5. **Enable Occlusion** if available

### Fix 2: Fix SegFormer Model Assignment
The system has SegFormer models but they're not being assigned correctly.

### Fix 3: Create Missing Paint Materials
Create the missing Matte, Satin, Gloss, and SemiGloss materials.

---

## 🔧 Automated Fix Available

I can create an automated fix script that will:
- ✅ Automatically assign the correct SegFormer model
- ✅ Create all missing paint materials
- ✅ Configure XR settings properly
- ✅ Test the entire system

**Would you like me to create the automated fix?**

---

## 📊 Available Resources Found:

### ✅ SegFormer Models Available:
- `segformer-b0-ade-512x512.onnx` ✅
- `segformer-b0-scene-parse-150.onnx` ✅  
- `segformer-b4-wall.onnx` ✅
- `segformer-b5-ade-640x640.onnx` ✅
- `segformer.b1.512x512.ade.160k.onnx` ✅

### ✅ Materials Available:
- Basic wall paint materials exist
- Need to create finish-specific variants

### ⚠️ XR Configuration:
- Needs proper AR provider setup
- Occlusion subsystem needs configuration

---

## 🎯 Next Steps:

1. **Run the automated fix** (recommended)
2. **Or manually apply fixes** following the guide above
3. **Test the system** after fixes

The system has all the necessary resources - just needs proper configuration!