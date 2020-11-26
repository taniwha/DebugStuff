export MODNAME		:= DebugStuff
export KSPDIR		:= ${HOME}/ksp/KSP_linux
export MANAGED		:= ${KSPDIR}/KSP_Data/Managed
export GAMEDATA		:= ${KSPDIR}/GameData
export MODGAMEDATA	:= ${GAMEDATA}/${MODNAME}
export PLUGINDIR	:= ${MODGAMEDATA}/Plugins

TARGETS		:= bin/DebugStuff.dll

IMAGE_PATH := Modules/KodeUI/KodeUI-Unity/GameData/KodeUI/UI

IMAGES := \
	${IMAGE_PATH}/background.png	\
	${IMAGE_PATH}/button.png	\
	${IMAGE_PATH}/button_on.png	\
	${IMAGE_PATH}/dropdown.png	\
	${IMAGE_PATH}/toggle.png	\
	${IMAGE_PATH}/toggle_off.png	\
	${IMAGE_PATH}/toggle_on.png	\
	${IMAGE_PATH}/window.png	\
	$e

DS_FILES := \
	DebugDrawer.cs \
	DebugStuff.cs \
	DrawTools.cs \
	SpriteViewer.cs \
	Properties/AssemblyInfo.cs \
	$e

include KodeUI/KodeUI.inc

RESGEN2		:= resgen2
CSC			:= csc
CSCFLAGS	:= -highentropyva- -noconfig -nostdlib+ -t:library -optimize -warnaserror -debug
GIT			:= git
TAR			:= tar
ZIP			:= zip

all: ${TARGETS}

.PHONY: version
version:
	@../tools/git-version.sh

info:
	@echo "${MODNAME} Build Information"
	@echo "    resgen2:    ${RESGEN2}"
	@echo "    csc:        ${CSC}"
	@echo "    csc flags:  ${CSCFLAGS}"
	@echo "    git:        ${GIT}"
	@echo "    tar:        ${TAR}"
	@echo "    zip:        ${ZIP}"
	@echo "    KSP Data:   ${KSPDIR}"

SYSTEM := \
	-lib:${MANAGED} \
	-r:${MANAGED}/mscorlib.dll \
	-r:${MANAGED}/System.dll \
	-r:${MANAGED}/System.Core.dll

KSP := \
	-r:Assembly-CSharp.dll \
	-r:Assembly-CSharp-firstpass.dll

UNITY := \
	-r:UnityEngine.dll \
	-r:UnityEngine.CoreModule.dll \
	-r:UnityEngine.UI.dll \
	-r:UnityEngine.UIModule.dll \
	-r:UnityEngine.IMGUIModule.dll \
	-r:UnityEngine.AnimationModule.dll \
	-r:UnityEngine.TextRenderingModule.dll \
	-r:UnityEngine.PhysicsModule.dll \
	-r:UnityEngine.InputLegacyModule.dll \
	-r:UnityEngine.Physics2DModule.dll \
	$e

bin/DebugStuff.dll: ${DS_FILES} ${KodeUI}
	@mkdir -p bin
	${CSC} ${CSCFLAGS} ${SYSTEM} ${KSP} ${UNITY} -out:$@ $^

clean:
	rm -f ${TARGETS}*
	test -d bin && rmdir bin || true

install: all
	mkdir -p ${PLUGINDIR}
	cp ${TARGETS} ${PLUGINDIR}
	mkdir -p ${MODGAMEDATA}/UI
	cp ${IMAGES} DefaultSkin.cfg ${MODGAMEDATA}/UI

.PHONY: all clean install
