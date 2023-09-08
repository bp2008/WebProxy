<template>
	<div class="circularMeter">
		<canvas ref="canvas" :style="{ width: canvasSize.w + 'px', height: canvasSize.h + 'px' }"></canvas>
		<div class="labelText" :style="{ bottom: textOffset + 'px' }">{{text}}</div>
	</div>
</template>

<script>
	export default {
		name: 'CircularMeter',
		props: {
			diameter: {
				type: Number,
				default: 200
			},
			degrees: {
				type: Number,
				default: 270,
				validator: value => value >= 0 && value <= 360
			},
			value: {
				type: Number,
				required: true,
				validator: value => value >= 0 && value <= 1
			},
			backgroundThickness: {
				type: Number,
				default: 20
			},
			foregroundThickness: {
				type: Number,
				default: 20
			},
			backgroundColor: {
				type: String,
				default: ''
			},
			foregroundColor: {
				type: String,
				default: '#00f'
			},
			segmentCount: {
				type: Number,
				default: 1
			},
			segmentSeparatorThickness: {
				type: Number,
				default: 2
			},
			segmentSeparatorColor: {
				type: String,
				default: ''
			},
			text: {
				type: String,
				default: ""
			},
			textOffset: {
				type: Number,
				default: 2,
				validator: value => value >= 0
			},
		},
		mounted()
		{
			this.drawMeter();
			window.addEventListener("resize", this.drawMeter);
		},
		beforeUnmount()
		{
			window.removeEventListener("resize", this.drawMeter);
		},
		watch:
		{
			'$props': {
				handler()
				{
					this.drawMeter();
				},
				deep: true
			},
			canvasSize()
			{
				this.$nextTick(this.drawMeter);
			}
		},
		computed:
		{
			startAngle()
			{
				return -Math.PI / 2 - (this.degrees * Math.PI) / 360
			},
			canvasSize()
			{
				const radius = this.diameter / 2;
				const startAngle = this.startAngle;
				let h;
				if (this.degrees > 180)
					h = radius + Math.sin(startAngle) * radius;
				else
				{
					let bThick = Math.min(this.backgroundThickness, this.diameter / 2);
					let fThick = Math.min(this.foregroundThickness, this.diameter / 2);
					const largerThickness = Math.max(bThick, fThick);
					h = radius + Math.sin(startAngle) * (radius - largerThickness);
				}
				if (h <= 0)
					h = this.diameter;
				return {
					w: this.diameter,
					h: Math.ceil(h)
				};
			}
		},
		methods:
		{
			drawMeter()
			{
				if (!this.$refs.canvas)
					return;
				try
				{
					const canvas = this.$refs.canvas;
					const ctx = canvas.getContext('2d');

					const dpr = window.devicePixelRatio || 1;
					const hiDpiW = this.canvasSize.w * dpr;
					const hiDpiH = this.canvasSize.h * dpr;
					if (canvas.width !== hiDpiW || canvas.height != hiDpiH)
					{
						canvas.width = hiDpiW;
						canvas.height = hiDpiH;
						ctx.scale(dpr, dpr);
					}

					ctx.clearRect(0, 0, this.canvasSize.w, this.canvasSize.h);

					let bgColor = this.backgroundColor;
					let sepColor = this.segmentSeparatorColor;
					if (!bgColor || !sepColor)
					{
						const darkTheme = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
						if (!bgColor)
							bgColor = darkTheme ? "#666" : "#ddd";
						if (!sepColor)
							sepColor = darkTheme ? "#000" : "#fff";
					}
					let bThick = Math.min(this.backgroundThickness, this.diameter / 2);
					let fThick = Math.min(this.foregroundThickness, this.diameter / 2);
					const radius = this.diameter / 2;
					const startAngle = this.startAngle;
					const endAngle = startAngle + (this.degrees * Math.PI) / 180;
					const valueAngle = startAngle + (endAngle - startAngle) * this.value;
					const largerThickness = Math.max(bThick, fThick);

					ctx.beginPath();
					ctx.arc(radius, radius, radius - largerThickness / 2, startAngle, endAngle);
					ctx.lineWidth = bThick;
					ctx.strokeStyle = bgColor;
					ctx.stroke();

					ctx.beginPath();
					ctx.arc(radius, radius, radius - largerThickness / 2, startAngle, valueAngle);
					ctx.lineWidth = fThick;
					ctx.strokeStyle = this.foregroundColor;
					ctx.stroke();

					if (this.segmentCount > 1)
					{
						const segmentAngle = (endAngle - startAngle) / (this.segmentCount);
						for (let i = 1; i < this.segmentCount; i++)
						{
							const angle = startAngle + segmentAngle * i;

							const segmentOnBackground = angle > valueAngle || bThick === largerThickness;
							const segmentThickness = segmentOnBackground ? bThick : fThick;
							const segmentOffset = segmentThickness === largerThickness ? 0 : ((largerThickness - segmentThickness) / 2);
							const segmentStartMultiplier = radius - segmentOffset;
							const segmentEndMultiplier = radius - segmentThickness - segmentOffset;

							const x1 = radius + Math.cos(angle) * segmentStartMultiplier;
							const y1 = radius + Math.sin(angle) * segmentStartMultiplier;
							const x2 = radius + Math.cos(angle) * segmentEndMultiplier;
							const y2 = radius + Math.sin(angle) * segmentEndMultiplier;

							ctx.beginPath();
							ctx.moveTo(x1, y1);
							ctx.lineTo(x2, y2);
							ctx.lineWidth = this.segmentSeparatorThickness;
							ctx.strokeStyle = sepColor;
							ctx.stroke();
						}
					}
				}
				catch (ex)
				{
					console.error(ex);
				}
			}
		}
	};
</script>

<style scoped>
	.circularMeter
	{
		position: relative;
		display: inline-block;
	}

	.labelText
	{
		position: absolute;
		bottom: 1px;
		left: 0px;
		width: 100%;
		text-align: center;
	}
</style>