<template>
	<div class="primaryContainer" :class="{ expanded, collapsed: !expanded }">
		<FloatingButtons @delete="$emit('delete')" />
		<div class="primaryContainerHeading"
			 tabindex="0"
			 @click="toggle"
			 @keypress.enter.space.prevent="toggle">
			<div class="title" v-if="expanded">{{title}}</div>
			<div class="name" v-else>{{name}}</div>
		</div>
		<transition name="expand" @before-enter="beforeEnter" @enter="enter" @leave="leave">
			<div v-if="expanded" class="primaryContainerContent">
				<slot></slot>
			</div>
		</transition>
	</div>
</template>

<script>
	import FloatingButtons from '/src/components/FloatingButtons.vue'
	import store from '/src/library/store';

	export default {
		components: { FloatingButtons },
		props:
		{
			title: {
				type: String,
				required: true
			},
			name: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
			};
		},
		created()
		{
		},
		computed:
		{
			myExpansionStateKey()
			{
				return this.title + "_" + this.name;
			},
			expanded()
			{
				return store.expansionState[this.myExpansionStateKey] !== "0";
			}
		},
		methods:
		{
			toggle()
			{
				store.expansionState[this.myExpansionStateKey] = store.expansionState[this.myExpansionStateKey] !== "0" ? "0" : "1";
			},
			beforeEnter(el)
			{
				el.style.height = '0';
			},
			enter(el, done)
			{
				const height = el.scrollHeight;
				el.style.height = height + 'px';
				const cleanup = () =>
				{
					el.style.height = null;
					el.removeEventListener('transitionend', cleanup);
					done();
				};
				el.addEventListener('transitionend', cleanup);
			},
			leave(el, done)
			{
				el.style.height = el.scrollHeight + 'px';
				el.offsetHeight; // force repaint
				el.style.height = '0';
				const cleanup = () =>
				{
					el.removeEventListener('transitionend', cleanup);
					done();
				};
				el.addEventListener('transitionend', cleanup);
			},
		},
	};
</script>

<style scoped>
	.primaryContainer
	{
		border: 1px solid #999999;
		border-radius: 5px;
		padding: 1em;
		margin-bottom: 0.25em;
		background-color: rgba(255,255,255,0.05);
		/*background: #13112E;*/
		/*background: radial-gradient(ellipse at bottom, #34437A33 0%, #13112E33 80%);*/
	}

		.primaryContainer.expanded
		{
			margin-bottom: 2em;
		}

		.primaryContainer:last-of-type
		{
			margin-bottom: 1em;
		}

		.primaryContainer.expanded > *
		{
			margin-bottom: 0.5em;
		}

		.primaryContainer > *:last-child,
		.dashedBorder > *:last-child
		{
			margin-bottom: 0em;
		}

	.primaryContainerHeading
	{
		position: relative;
		top: -10px;
		left: -5px;
		/*text-decoration: underline;*/
		font-family: Verdana;
		/*display: inline-block;*/
		cursor: pointer;
		user-select: none;
	}

		.primaryContainerHeading:hover
		{
			background-color: rgba(0,0,0,0.06667);
		}

		.primaryContainerHeading .title
		{
			font-size: 1.5em;
			font-style: italic;
			font-weight: bold;
		}

		.primaryContainerHeading .name
		{
			color: var(--text-color);
		}

	.expand-enter-active, .expand-leave-active
	{
		transition: height 0.1s ease;
		overflow: hidden;
	}

	.expand-enter, .expand-leave-to /* .expand-leave-active in <2.1.8 */
	{
		height: 0;
		overflow: hidden;
	}
</style>
