<template>
	<div>
		<div class="uploadInput button">
			{{label}} <input ref="fileInput" type="file" :accept="acceptFileExtension" @change="fileInputChangeCounter++" />
		</div>
		<span v-if="selectedFile" class="selectedFileName">{{selectedFile.name}}</span>
		<div v-if="selectedFile" class="uploadBtn button" @click="uploadClicked()">Upload <UploadIcon /></div>
	</div>
</template>

<script>
	import UploadIcon from '/src/assets/upload.svg?component'

	export default {
		components: { UploadIcon },
		props:
		{
			label: {
				type: String,
				required: true
			},
			acceptFileExtension: {
				type: String,
				default: "",
			}
		},
		data()
		{
			return {
				fileInputChangeCounter: 0,
			};
		},
		computed:
		{
			selectedFile()
			{
				if (this.fileInputChangeCounter > 0 && this.$refs.fileInput.files.length > 0)
					return this.$refs.fileInput.files[0];
				return null;
			}
		},
		watch:
		{
		},
		methods:
		{
			async uploadClicked()
			{
				if (this.selectedFile)
				{
					if (this.acceptFileExtension && !this.selectedFile.name.toLowerCase().endsWith(this.acceptFileExtension.toLowerCase()))
					{
						toaster.error('File extension is not "' + this.acceptFileExtension + '"');
						return;
					}
					let arrayBuffer = await this.selectedFile.arrayBuffer();
					let base64 = _arrayBufferToBase64(arrayBuffer);
					this.$emit("upload", base64);
				}
				else
					toaster.error('No file is selected');
			}
		}
	};
	function _arrayBufferToBase64(buffer)
	{
		var binary = '';
		var bytes = new Uint8Array(buffer);
		var len = bytes.byteLength;
		for (var i = 0; i < len; i++)
			binary += String.fromCharCode(bytes[i]);
		return window.btoa(binary);
	}
</script>

<style scoped>
	.uploadInput
	{
		position: relative;
		display: inline-block;
	}

		.uploadInput input[type="file"]
		{
			position: absolute;
			left: 0;
			opacity: 0;
			top: 0;
			bottom: 0;
			width: 100%;
			cursor: pointer;
		}

	.selectedFileName
	{
		padding: 0px 10px;
	}

	.uploadBtn
	{
		position: relative;
		fill: currentColor;
		display: inline-flex;
		align-items: center;
	}

		.uploadBtn svg
		{
			width: 20px;
			height: 20px;
			margin-left: 5px;
		}
</style>
