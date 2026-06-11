<script setup lang="ts">
import { computed } from 'vue';
import * as store from './store/gameStore';
import LobbyJoin from './views/LobbyJoin.vue';
import GameView from './views/GameView.vue';

const currentView = computed(() => {
  if (store.phase.value === 'Join') {
    return LobbyJoin;
  }
  return GameView;
});
</script>

<template>
  <main class="app-main">
    <Transition name="slide" mode="out-in">
      <component :is="currentView" />
    </Transition>
  </main>
</template>

<style scoped>
.app-main {
  width: 100%;
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
}

.slide-enter-active,
.slide-leave-active {
  transition: all 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.slide-enter-from {
  opacity: 0;
  transform: translateX(30px);
}

.slide-leave-to {
  opacity: 0;
  transform: translateX(-30px);
}
</style>
